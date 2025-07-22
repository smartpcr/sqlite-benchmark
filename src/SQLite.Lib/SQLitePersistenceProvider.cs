// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Traces;

    /// <summary>
    /// SQLite implementation of IPersistenceProvider that translates CRUD operations to SQL.
    /// </summary>
    public class SQLitePersistenceProvider<T, TKey> : IPersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly string connectionString;
        private readonly Models.VersionMapper versionMapper;
        private readonly Models.EntryListMappingMapper entryListMappingMapper;
        private readonly string tableName;

        public ISQLiteEntityMapper<T, TKey> Mapper { get; private set; }

        public SQLitePersistenceProvider(
            string connectionString,
            ISQLiteEntityMapper<T, TKey> mapper,
            Models.VersionMapper versionMapper,
            Models.EntryListMappingMapper entryListMappingMapper)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.versionMapper = versionMapper ?? throw new ArgumentNullException(nameof(versionMapper));
            this.entryListMappingMapper = entryListMappingMapper ?? throw new ArgumentNullException(nameof(entryListMappingMapper));
            this.tableName = this.Mapper.GetTableName();
        }

        #region CRUD Operations - Translating Func delegates to SQL

        /// <summary>
        /// Implements Get = Func&lt;TKey, T&gt; by translating to parameterized SQL SELECT.
        /// Returns the latest version of the entity (highest version number).
        /// </summary>
        public async Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(key);
            
            Logger.GetStart(keyString, this.tableName);
            
            try
            {
                // Translate Get = Func<TKey, T> to SQL
                // Order by Version DESC to get the latest version first
                // IMPORTANT: We do NOT filter by IsDeleted = 0 here because:
                // 1. We need to return the latest version regardless of deletion status
                // 2. The caller needs to know if an entity was deleted (by checking IsDeleted flag)
                // 3. Filtering would hide deleted entities and make it impossible to distinguish
                //    between "never existed" and "was deleted"
                var sql = $@"
                    SELECT {this.Mapper.GetSelectColumns()}
                    FROM {this.tableName}
                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                    ORDER BY Version DESC
                    LIMIT 1";

                T result = null;

                using var connection = new SQLiteConnection(this.connectionString);
                // Always pass cancellation token to OpenAsync for proper cancellation support
                await connection.OpenAsync(cancellationToken);

                using var command = this.Mapper.CreateCommand(DbOperationType.Select, key, null, null);
                command.Connection = connection;

                long? version = null;

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var foundEntity = this.Mapper.MapFromReader(reader);

                    // Check if the entity is marked as deleted
                    if (foundEntity != null)
                    {
                        version = foundEntity.Version;
                        var isDeleted = foundEntity.IsDeleted;

                        result = isDeleted
                            ? null
                            : foundEntity;
                    }
                }
                
                stopwatch.Stop();
                
                if (result == null)
                {
                    Logger.GetNotFound(keyString, this.tableName);
                }
                else
                {
                    Logger.GetStop(keyString, this.tableName, stopwatch);
                    
                    if (result != null)
                    {
                        Logger.CacheHit(keyString);
                    }
                    else
                    {
                        Logger.CacheMiss(keyString);
                    }
                }

                // Populate CacheAccessHistory for all access attempts
                if (callerInfo != null)
                {
                    try
                    {
                        var historyInsertSql = @"
                            INSERT INTO CacheAccessHistory (
                                CacheKey, TypeName, Operation, CacheHit, Version,
                                CallerFile, CallerMember, CallerLineNumber, Timestamp
                            ) VALUES (
                                @key, @typeName, 'GET', @cacheHit, @version,
                                @filePath, @memberName, @lineNumber, datetime('now')
                            )";

                        // Use a separate connection for audit logging to ensure:
                        // 1. Audit failures don't affect the main operation
                        // 2. Avoid conflicts with active DataReaders on the main connection
                        // 3. Maintain consistent pattern with transactional operations where
                        //    audit logs must be written outside the main transaction
                        // 4. Allow immediate release of the main connection back to the pool
                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync(cancellationToken);

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        historyCommand.Parameters.AddWithValue("@key", keyString);
                        historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@cacheHit", result != null ? 1 : 0);
                        historyCommand.Parameters.AddWithValue("@version", version ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);

                        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.GetFailed(keyString, this.tableName, stopwatch, ex);
                throw;
            }
        }

        /// <summary>
        /// Implements Create = Func&lt;T, T&gt; by translating to parameterized SQL INSERT.
        /// </summary>
        public async Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(entity.Id);
            
            Logger.CreateStart(keyString, this.tableName);
            
            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

                // Note: SQLite transactions use synchronous Commit/Rollback methods
                // as the underlying SQLite library doesn't provide true async transaction operations
                using var transaction = connection.BeginTransaction();
                try
                {
                // Step 1: Check if entity already exists and is not deleted
                var checkExistsSql = $@"
                    SELECT {this.Mapper.GetSelectColumns()}
                    FROM {this.tableName}
                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                    ORDER BY Version DESC
                    LIMIT 1";

                using var checkCommand = new SQLiteCommand(checkExistsSql, connection, transaction);
                checkCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));

                using var checkReader = await checkCommand.ExecuteReaderAsync(cancellationToken);
                if (await checkReader.ReadAsync(cancellationToken))
                {
                    var existingEntity = this.Mapper.MapFromReader(checkReader);
                    if (!existingEntity.IsDeleted)
                    {
                        throw new EntityAlreadyExistsException(
                            entity.Id.ToString(),
                            $"Entity with key '{entity.Id}' already exists. Use UpdateAsync to modify existing entities.");
                    }
                }

                // Set tracking fields before insert
                entity.CreatedTime = DateTimeOffset.UtcNow;
                entity.LastWriteTime = entity.CreatedTime;

                // Get column names and parameter names
                var columns = this.Mapper.GetInsertColumns();
                var parameters = columns.Select(c => $"@{c}").ToList();

                // Step 2: Insert into Version table to get next version
                using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                versionCommand.Connection = connection;
                versionCommand.Transaction = transaction;
                var version = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                entity.Version = version;

                // Step 3: Insert entity with the version from step 2
                var insertEntitySql = $@"
                    INSERT INTO {this.tableName} ({string.Join(", ", columns)})
                    VALUES ({string.Join(", ", parameters)});";

                using var insertCommand = new SQLiteCommand(insertEntitySql, connection, transaction);
                this.Mapper.AddParameters(insertCommand, entity);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);

                // Step 4: Retrieve the inserted entity
                var selectSql = $@"
                    SELECT {this.Mapper.GetSelectColumns()}
                    FROM {this.tableName}
                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                      AND Version = @version;";

                using var selectCommand = new SQLiteCommand(selectSql, connection, transaction);
                selectCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                selectCommand.Parameters.AddWithValue("@version", version);

                using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var result = this.Mapper.MapFromReader(reader);
                    transaction.Commit();

                    // Populate CacheUpdateHistory after commit
                    if (callerInfo != null)
                    {
                        try
                        {
                            var historyInsertSql = @"
                                INSERT INTO CacheUpdateHistory (
                                    CacheKey, TypeName, Operation, Version, Size,
                                    CallerFilePath, CallerMemberName, CallerLineNumber, UpdateTime
                                ) VALUES (
                                    @key, @typeName, 'INSERT', @version, @size,
                                    @filePath, @memberName, @lineNumber, datetime('now')
                                )";

                            // Use a separate connection for audit logging to ensure:
                            // 1. Audit logs are written AFTER the main transaction commits
                            // 2. Audit log failures don't roll back the main operation
                            // 3. Audit logs persist even if main transaction rolls back
                            // 4. Compliance: audit trails remain immutable and independent
                            using var historyConnection = new SQLiteConnection(this.connectionString);
                            await historyConnection.OpenAsync(cancellationToken);

                            using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                            historyCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                            historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                            historyCommand.Parameters.AddWithValue("@version", version);
                            historyCommand.Parameters.AddWithValue("@size", this.EstimateEntitySize(entity));
                            historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                            historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                            historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);

                            await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                        catch
                        {
                            // Log but don't fail the operation if audit fails
                        }
                    }

                    stopwatch.Stop();
                    Logger.CreateStop(keyString, this.tableName, stopwatch);
                    return result;
                }

                throw new InvalidOperationException("Failed to retrieve created entity");
            }
            catch (EntityAlreadyExistsException)
            {
                stopwatch.Stop();
                Logger.CreateFailed(keyString, this.tableName, stopwatch, new InvalidOperationException($"Entity with key '{keyString}' already exists"));
                // Transaction rollback will undo both Version and entity inserts
                transaction.Rollback();
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.CreateFailed(keyString, this.tableName, stopwatch, ex);
                // Transaction rollback will undo both Version and entity inserts
                transaction.Rollback();
                throw;
            }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.CreateFailed(keyString, this.tableName, stopwatch, ex);
                throw;
            }
        }

        /// <summary>
        /// Implements Update = Func&lt;T, T&gt; by translating to parameterized SQL UPDATE.
        /// </summary>
        public async Task<T> UpdateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(entity.Id);
            
            Logger.UpdateStart(keyString, this.tableName);
            
            try
            {
                // Store original version for optimistic concurrency check
                var originalVersion = entity.Version;

                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                // Step 1: Get the old value for history tracking
                T oldValue = default(T);
                if (callerInfo != null)
                {
                    // Note: Here we DO filter by IsDeleted = 0 because we're verifying
                    // the entity exists and matches the expected version for update
                    var selectOldSql = $@"
                        SELECT {this.Mapper.GetSelectColumns()}
                        FROM {this.tableName}
                        WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                          AND Version = @originalVersion
                          AND IsDeleted = 0";

                    using var selectOldCommand = new SQLiteCommand(selectOldSql, connection, transaction);
                    selectOldCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                    selectOldCommand.Parameters.AddWithValue("@originalVersion", originalVersion);

                    using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                    if (await oldReader.ReadAsync(cancellationToken))
                    {
                        oldValue = this.Mapper.MapFromReader(oldReader);
                    }
                }

                // Step 2: Insert into Version table to get next version
                using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                versionCommand.Connection = connection;
                versionCommand.Transaction = transaction;
                var newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));

                // Update tracking fields
                entity.LastWriteTime = DateTimeOffset.UtcNow;
                entity.Version = newVersion;

                // Build SET clause with all updatable columns
                var updateColumns = this.Mapper.GetUpdateColumns()
                    .Select(c => $"{c} = @{c}")
                    .ToList();

                // Step 2: Update entity with new version and optimistic concurrency check
                var updateSql = $@"
                    UPDATE {this.tableName}
                    SET {string.Join(", ", updateColumns)}
                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                      AND Version = @originalVersion
                      AND IsDeleted = 0;

                    SELECT changes();";

                using var updateCommand = new SQLiteCommand(updateSql, connection, transaction);

                // Add all parameters including concurrency check
                this.Mapper.AddParameters(updateCommand, entity);
                updateCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                updateCommand.Parameters.AddWithValue("@originalVersion", originalVersion);

                var rowsAffected = Convert.ToInt32(await updateCommand.ExecuteScalarAsync(cancellationToken));

                if (rowsAffected == 0)
                {
                    throw new ConcurrencyException(
                        $"Entity with key '{entity.Id}' has been modified by another process.");
                }

                transaction.Commit();

                // Populate CacheUpdateHistory after commit
                if (callerInfo != null)
                {
                    try
                    {
                        var historyInsertSql = @"
                            INSERT INTO CacheUpdateHistory (
                                CacheKey, TypeName, Operation, Version, OldVersion, Size,
                                CallerFilePath, CallerMemberName, CallerLineNumber, UpdateTime
                            ) VALUES (
                                @key, @typeName, 'UPDATE', @version, @oldVersion, @size,
                                @filePath, @memberName, @lineNumber, datetime('now')
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync(cancellationToken);

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        historyCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                        historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@version", newVersion);
                        historyCommand.Parameters.AddWithValue("@oldVersion", originalVersion);
                        historyCommand.Parameters.AddWithValue("@size", this.EstimateEntitySize(entity));
                        historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);

                        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                stopwatch.Stop();
                Logger.UpdateStop(keyString, this.tableName, stopwatch);
                return entity;
            }
            catch (ConcurrencyException)
            {
                stopwatch.Stop();
                Logger.UpdateConcurrencyConflict(keyString, this.tableName);
                transaction.Rollback();
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.UpdateFailed(keyString, this.tableName, stopwatch, ex);
                transaction.Rollback();
                throw;
            }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.UpdateFailed(keyString, this.tableName, stopwatch, ex);
                throw;
            }
        }

        /// <summary>
        /// Implements Delete = Func&lt;TKey, bool&gt; by translating to SQL UPDATE (soft delete) or DELETE.
        /// </summary>
        public async Task<bool> DeleteAsync(TKey key, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(key);
            
            Logger.DeleteStart(keyString, this.tableName);
            
            try
            {
                string sql;

            if (hardDelete)
            {
                // Translate to SQL DELETE for hard delete
                sql = $@"
                    DELETE FROM {this.tableName}
                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key;

                    SELECT changes();";
            }
            else
            {
                // Translate to SQL UPDATE for soft delete
                // Note: Version is NOT incremented for soft delete
                sql = $@"
                    UPDATE {this.tableName}
                    SET IsDeleted = 1,
                        LastWriteTime = @lastWriteTime
                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                      AND IsDeleted = 0;

                    SELECT changes();";
            }

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                // Get the old value for history tracking
                T oldValue = default(T);
                long version = 0;

                if (callerInfo != null)
                {
                    var selectOldSql = $@"
                        SELECT {this.Mapper.GetSelectColumns()}
                        FROM {this.tableName}
                        WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                        ORDER BY Version DESC
                        LIMIT 1";

                    using var selectOldCommand = new SQLiteCommand(selectOldSql, connection, transaction);
                    selectOldCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(key));

                    using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                    if (await oldReader.ReadAsync(cancellationToken))
                    {
                        oldValue = this.Mapper.MapFromReader(oldReader);
                        version = oldValue.Version;

                        // Only proceed with audit if the entity wasn't already deleted
                        if (oldValue.IsDeleted)
                        {
                            oldValue = default(T);
                        }
                    }
                }

                using var command = new SQLiteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(key));

                if (!hardDelete)
                {
                    command.Parameters.AddWithValue("@lastWriteTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }

                var rowsAffected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                transaction.Commit();

                // Populate CacheUpdateHistory after commit
                if (rowsAffected > 0 && callerInfo != null)
                {
                    try
                    {
                        var historyInsertSql = @"
                            INSERT INTO CacheUpdateHistory (
                                CacheKey, TypeName, Operation, Version, OldVersion, Size,
                                CallerFilePath, CallerMemberName, CallerLineNumber, UpdateTime
                            ) VALUES (
                                @key, @typeName, 'DELETE', @version, @oldVersion, @size,
                                @filePath, @memberName, @lineNumber, datetime('now')
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync(cancellationToken);

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        historyCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(key));
                        historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@version", version);
                        historyCommand.Parameters.AddWithValue("@oldVersion", oldValue?.Version ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@size", this.EstimateEntitySize(oldValue));
                        historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);

                        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                stopwatch.Stop();
                Logger.DeleteStop(keyString, this.tableName, stopwatch);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.DeleteFailed(keyString, this.tableName, stopwatch, ex);
                transaction.Rollback();
                throw;
            }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.DeleteFailed(keyString, this.tableName, stopwatch, ex);
                throw;
            }
        }

        #endregion

        #region batch operations
        public async Task<IEnumerable<T>> CreateBatchAsync(IEnumerable<T> entities, string listCacheKey, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var entityList = entities?.ToList() ?? new List<T>();
            if (!entityList.Any())
            {
                return Enumerable.Empty<T>();
            }

            var stopwatch = Stopwatch.StartNew();
            Logger.BatchOperationStart("CreateBatch", entityList.Count, listCacheKey);
            
            try
            {
                var results = new List<T>();

                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                // Get next version for all entities in the batch
                long version;
                using (var versionCmd = this.versionMapper.CreateGetNextVersionCommand())
                {
                    versionCmd.Connection = connection;
                    versionCmd.Transaction = transaction;
                    var versionResult = await versionCmd.ExecuteScalarAsync(cancellationToken);
                    version = Convert.ToInt64(versionResult);
                }

                // Use injected EntryListMapping mapper

                // Process each entity
                foreach (var entity in entityList)
                {
                    // Set version for the entity
                    entity.Version = version;

                    // Insert entity into main table
                    using (var insertCmd = this.Mapper.CreateCommand(DbOperationType.Insert, entity.Id, entity, null))
                    {
                        insertCmd.Connection = connection;
                        insertCmd.Transaction = transaction;
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Create EntryListMapping record
                    var listMapping = new Models.EntryListMapping
                    {
                        ListCacheKey = listCacheKey,
                        EntryCacheKey = entity.Id.ToString(),
                        Version = version,
                        CreatedTime = DateTimeOffset.UtcNow,
                        LastWriteTime = DateTimeOffset.UtcNow,
                        CallerFile = callerInfo?.CallerFilePath,
                        CallerMember = callerInfo?.CallerMemberName,
                        CallerLineNumber = callerInfo?.CallerLineNumber
                    };

                    using (var listMappingCmd = this.entryListMappingMapper.CreateCommand(DbOperationType.Insert, null, listMapping, null))
                    {
                        listMappingCmd.Connection = connection;
                        listMappingCmd.Transaction = transaction;
                        await listMappingCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    results.Add(entity);
                }

                transaction.Commit();
                stopwatch.Stop();
                Logger.BatchOperationStop("CreateBatch", results.Count, listCacheKey, stopwatch);
                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("CreateBatch", listCacheKey, stopwatch, ex);
                transaction.Rollback();
                throw;
            }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("CreateBatch", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetBatchAsync(string listCacheKey, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.BatchOperationStart("GetBatch", 0, listCacheKey); // Count unknown at start
            
            try
            {
                var results = new List<T>();

                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

            var keysList = new List<TKey>();
            using (var listCmd = this.entryListMappingMapper.CreateSelectByListKeyCommand(listCacheKey))
            {
                listCmd.Connection = connection;
                using var listReader = await listCmd.ExecuteReaderAsync(cancellationToken);
                if (await listReader.ReadAsync(cancellationToken))
                {
                    var mapping = this.entryListMappingMapper.MapFromReader(listReader);
                    if (mapping != null)
                    {
                        var key = this.Mapper.DeserializeKey(mapping.EntryCacheKey);
                        keysList.Add(key);
                    }
                }
            }

            foreach (var key in keysList)
            {
                using var command = this.Mapper.CreateCommand(DbOperationType.Select, key, null, null);
                command.Connection = connection;
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var entity = this.Mapper.MapFromReader(reader);
                    if (entity != null && !entity.IsDeleted)
                    {
                        results.Add(entity);
                    }
                }
            }

                stopwatch.Stop();
                Logger.BatchOperationStop("GetBatch", results.Count, listCacheKey, stopwatch);
                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("GetBatch", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> UpdateBatchAsync(IEnumerable<T> entities, string listCacheKey, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var entityList = entities?.ToList() ?? new List<T>();
            if (!entityList.Any())
            {
                return Enumerable.Empty<T>();
            }

            var stopwatch = Stopwatch.StartNew();
            Logger.BatchOperationStart("UpdateBatch", entityList.Count, listCacheKey);
            
            try
            {
                var results = new List<T>();

                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                // Get next version for all entities in the batch
                long newVersion;
                using (var versionCmd = this.versionMapper.CreateGetNextVersionCommand())
                {
                    versionCmd.Connection = connection;
                    versionCmd.Transaction = transaction;
                    var versionResult = await versionCmd.ExecuteScalarAsync(cancellationToken);
                    newVersion = Convert.ToInt64(versionResult);
                }

                // Use injected EntryListMapping mapper

                // First, delete existing list mappings
                using (var deleteListCmd = this.entryListMappingMapper.CreateDeleteByListKeyCommand(listCacheKey))
                {
                    deleteListCmd.Connection = connection;
                    deleteListCmd.Transaction = transaction;
                    await deleteListCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Process each entity
                foreach (var entity in entityList)
                {
                    // There is no need to check current entity since we always create a new version
                    entity.Version = newVersion;

                    // Insert new version
                    using (var insertCmd = this.Mapper.CreateCommand(DbOperationType.Insert, entity.Id, entity, null))
                    {
                        insertCmd.Connection = connection;
                        insertCmd.Transaction = transaction;
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Create new EntryListMapping record
                    var listMapping = new Models.EntryListMapping
                    {
                        ListCacheKey = listCacheKey,
                        EntryCacheKey = entity.Id.ToString(),
                        Version = newVersion,
                        CreatedTime = DateTimeOffset.UtcNow,
                        LastWriteTime = DateTimeOffset.UtcNow,
                        CallerFile = callerInfo?.CallerFilePath,
                        CallerMember = callerInfo?.CallerMemberName,
                        CallerLineNumber = callerInfo?.CallerLineNumber
                    };

                    using (var listMappingCmd = this.entryListMappingMapper.CreateCommand(DbOperationType.Insert, null, listMapping, null))
                    {
                        listMappingCmd.Connection = connection;
                        listMappingCmd.Transaction = transaction;
                        await listMappingCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    results.Add(entity);
                }

                transaction.Commit();
                stopwatch.Stop();
                Logger.BatchOperationStop("UpdateBatch", results.Count, listCacheKey, stopwatch);
                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("UpdateBatch", listCacheKey, stopwatch, ex);
                transaction.Rollback();
                throw;
            }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("UpdateBatch", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        public async Task<int> DeleteBatchAsync(string listCacheKey, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.BatchOperationStart("DeleteBatch", 0, listCacheKey); // Count unknown at start
            
            try
            {
                var deletedCount = 0;

                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                // Use injected EntryListMapping mapper

                // Delete existing list mappings
                using (var deleteListCmd = this.entryListMappingMapper.CreateDeleteByListKeyCommand(listCacheKey))
                {
                    deleteListCmd.Connection = connection;
                    deleteListCmd.Transaction = transaction;
                    await deleteListCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                transaction.Commit();
                stopwatch.Stop();
                Logger.BatchOperationStop("DeleteBatch", deletedCount, listCacheKey, stopwatch);
                return deletedCount;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("DeleteBatch", listCacheKey, stopwatch, ex);
                transaction.Rollback();
                throw;
            }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BatchOperationFailed("DeleteBatch", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        #endregion

        #region Query operations

        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.QueryStart(this.tableName);
            
            try
            {
                var results = new List<T>();

                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

            // Translate the expression to SQL
            var translator = new Mappings.SQLiteExpressionTranslator<T>(
                this.Mapper.GetPropertyMappings(),
                () => this.Mapper.GetPrimaryKeyColumn());
            var translationResult = translator.Translate(predicate);

            // Build the query
            var sql = $@"
                SELECT {string.Join(", ", this.Mapper.GetSelectColumns())}
                FROM {this.tableName}
                WHERE {translationResult.Sql}
                  AND IsDeleted = 0
                ORDER BY Version DESC";

            using var command = new SQLiteCommand(sql, connection);

            // Add parameters from the translation
            foreach (var param in translationResult.Parameters)
            {
                command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
            }

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Track the latest version for each unique key
            var latestVersions = new Dictionary<TKey, long>();
            var entities = new List<T>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var entity = this.Mapper.MapFromReader(reader);
                if (entity != null)
                {
                    // Check if we already have this key and if this version is newer
                    if (!latestVersions.ContainsKey(entity.Id) || entity.Version > latestVersions[entity.Id])
                    {
                        latestVersions[entity.Id] = entity.Version;

                        // Remove any previous version of this entity
                        entities.RemoveAll(e => e.Id.Equals(entity.Id));
                        entities.Add(entity);
                    }
                }
            }

            // Record access history for query operation if needed
            if (callerInfo != null)
            {
                try
                {
                    var historyInsertSql = @"
                        INSERT INTO CacheAccessHistory (
                            CacheKey, TypeName, Operation, CacheHit, Version,
                            CallerFile, CallerMember, CallerLineNumber, Timestamp
                        ) VALUES (
                            @key, @typeName, @operation, @cacheHit, @version,
                            @callerFile, @callerMember, @callerLineNumber, @timestamp
                        )";

                    using var historyConnection = new SQLiteConnection(this.connectionString);
                    await historyConnection.OpenAsync(cancellationToken);

                    using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                    historyCommand.Parameters.AddWithValue("@key", $"query-{translationResult.Sql.GetHashCode()}");
                    historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                    historyCommand.Parameters.AddWithValue("@operation", "Query");
                    historyCommand.Parameters.AddWithValue("@cacheHit", entities.Count > 0 ? 1 : 0);
                    historyCommand.Parameters.AddWithValue("@version", DBNull.Value);
                    historyCommand.Parameters.AddWithValue("@callerFile", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                    historyCommand.Parameters.AddWithValue("@callerMember", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                    historyCommand.Parameters.AddWithValue("@callerLineNumber", callerInfo.CallerLineNumber);
                    historyCommand.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffzzz"));

                    await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                catch
                {
                    // Ignore history recording failures
                }
            }

                stopwatch.Stop();
                Logger.QueryStop(this.tableName, entities.Count, stopwatch);
                
                // Check for slow queries (> 1 second)
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    Logger.SlowQuery(this.tableName, stopwatch.ElapsedMilliseconds);
                }
                
                return entities;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.QueryFailed(this.tableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<PagedResult<T>> QueryPagedAsync(
            Expression<Func<T, bool>> predicate,
            int pageSize,
            int pageNumber,
            Expression<Func<T, IComparable>> orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            if (pageSize <= 0)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            if (pageNumber <= 0)
                throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));

            var stopwatch = Stopwatch.StartNew();
            Logger.QueryStart(this.tableName);
            
            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

            // Translate the predicate expression to SQL
            var translator = new Mappings.SQLiteExpressionTranslator<T>(
                this.Mapper.GetPropertyMappings(),
                () => this.Mapper.GetPrimaryKeyColumn());
            var translationResult = translator.Translate(predicate);

            // First, get the total count of matching records (only counting latest versions)
            var countSql = $@"
                WITH LatestVersions AS (
                    SELECT {this.Mapper.GetPrimaryKeyColumn()}, MAX(Version) as MaxVersion
                    FROM {this.tableName}
                    WHERE {translationResult.Sql}
                      AND IsDeleted = 0
                    GROUP BY {this.Mapper.GetPrimaryKeyColumn()}
                )
                SELECT COUNT(*) FROM LatestVersions";

            long totalCount;
            using (var countCommand = new SQLiteCommand(countSql, connection))
            {
                // Add parameters for count query
                foreach (var param in translationResult.Parameters)
                {
                    countCommand.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }

                var countResult = await countCommand.ExecuteScalarAsync(cancellationToken);
                totalCount = Convert.ToInt64(countResult);
            }

            // Build ORDER BY clause using the translator
            var orderByClause = "ORDER BY ";
            var orderByTranslation = translator.TranslateOrderBy(orderBy, ascending);
            if (!string.IsNullOrEmpty(orderByTranslation))
            {
                orderByClause += $"{orderByTranslation}, ";
            }
            // Always add Version DESC as secondary sort to ensure consistent ordering
            orderByClause += "Version DESC";

            // Calculate offset for pagination
            var offset = (pageNumber - 1) * pageSize;

            // Build the main query with pagination
            var sql = $@"
                WITH LatestVersions AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY {this.Mapper.GetPrimaryKeyColumn()} ORDER BY Version DESC) as rn
                    FROM {this.tableName}
                    WHERE {translationResult.Sql}
                      AND IsDeleted = 0
                )
                SELECT {string.Join(", ", this.Mapper.GetSelectColumns().Select(c => $"lv.{c}"))}
                FROM LatestVersions lv
                WHERE lv.rn = 1
                {orderByClause}
                LIMIT @pageSize OFFSET @offset";

            var items = new List<T>();
            using (var command = new SQLiteCommand(sql, connection))
            {
                // Add parameters from the translation
                foreach (var param in translationResult.Parameters)
                {
                    command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }

                // Add pagination parameters
                command.Parameters.AddWithValue("@pageSize", pageSize);
                command.Parameters.AddWithValue("@offset", offset);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var entity = this.Mapper.MapFromReader(reader);
                    if (entity != null)
                    {
                        items.Add(entity);
                    }
                }
            }

                stopwatch.Stop();
                Logger.QueryStop(this.tableName, items.Count, stopwatch);
                
                // Check for slow queries (> 1 second)
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    Logger.SlowQuery(this.tableName, stopwatch.ElapsedMilliseconds);
                }

                return new PagedResult<T>
                {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.QueryFailed(this.tableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<long> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.QueryStart(this.tableName);
            
            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

            string whereClause = "1=1"; // Default condition if no predicate
            var parameters = new Dictionary<string, object>();

            // If predicate is provided, translate it to SQL
            if (predicate != null)
            {
                var translator = new Mappings.SQLiteExpressionTranslator<T>(
                    this.Mapper.GetPropertyMappings(),
                    () => this.Mapper.GetPrimaryKeyColumn());
                var translationResult = translator.Translate(predicate);
                whereClause = translationResult.Sql;
                parameters = translationResult.Parameters;
            }

            // Count only the latest version of each entity (excluding soft-deleted)
            var sql = $@"
                WITH LatestVersions AS (
                    SELECT {this.Mapper.GetPrimaryKeyColumn()}, MAX(Version) as MaxVersion
                    FROM {this.tableName}
                    WHERE {whereClause}
                      AND IsDeleted = 0
                    GROUP BY {this.Mapper.GetPrimaryKeyColumn()}
                )
                SELECT COUNT(*) FROM LatestVersions";

            using var command = new SQLiteCommand(sql, connection);

            // Add parameters from the translation
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
            }

                var result = await command.ExecuteScalarAsync(cancellationToken);
                stopwatch.Stop();
                Logger.QueryStop(this.tableName, 1, stopwatch); // Count queries return single value
                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.QueryFailed(this.tableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var stopwatch = Stopwatch.StartNew();
            Logger.QueryStart(this.tableName);
            
            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

            // Translate the predicate expression to SQL
            var translator = new Mappings.SQLiteExpressionTranslator<T>(
                this.Mapper.GetPropertyMappings(),
                () => this.Mapper.GetPrimaryKeyColumn());
            var translationResult = translator.Translate(predicate);

            // Check if any entity exists matching the predicate (only checking latest versions)
            var sql = $@"
                SELECT EXISTS (
                    SELECT 1
                    FROM {this.tableName} t1
                    WHERE {translationResult.Sql}
                      AND IsDeleted = 0
                      AND Version = (
                          SELECT MAX(Version)
                          FROM {this.tableName} t2
                          WHERE t2.{this.Mapper.GetPrimaryKeyColumn()} = t1.{this.Mapper.GetPrimaryKeyColumn()}
                      )
                    LIMIT 1
                )";

            using var command = new SQLiteCommand(sql, connection);

            // Add parameters from the translation
            foreach (var param in translationResult.Parameters)
            {
                command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
            }

                var result = await command.ExecuteScalarAsync(cancellationToken);
                stopwatch.Stop();
                Logger.QueryStop(this.tableName, 1, stopwatch); // Exists queries return single value
                return Convert.ToBoolean(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.QueryFailed(this.tableName, stopwatch, ex);
                throw;
            }
        }

        #endregion

        #region Bulk operations

        public async Task<BulkImportResult> BulkImportAsync(
            IEnumerable<T> entities,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BulkImportOptions();
            var result = new BulkImportResult();
            var startTime = DateTime.UtcNow;

            if (entities == null)
            {
                result.Errors.Add("Entities collection is null");
                return result;
            }

            var entityList = entities.ToList();
            var totalCount = entityList.Count;

            if (totalCount == 0)
            {
                return result;
            }

            var stopwatch = Stopwatch.StartNew();
            Logger.BulkOperationStart("Import", totalCount);
            
            try
            {

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get next version for all entities
            long version;
            using (var versionCmd = this.versionMapper.CreateGetNextVersionCommand())
            {
                versionCmd.Connection = connection;
                var versionResult = await versionCmd.ExecuteScalarAsync(cancellationToken);
                version = Convert.ToInt64(versionResult);
            }

            // Process entities in batches
            var processedCount = 0;
            for (var i = 0; i < totalCount; i += options.BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = entityList.Skip(i).Take(options.BatchSize).ToList();

                foreach (var entity in batch)
                {
                    try
                    {
                        // Validate entity if requested
                        if (options.ValidateBeforeImport && entity == null)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Entity at index {processedCount} is null");
                            processedCount++;
                            continue;
                        }

                        // Check for duplicates if needed
                        if (!options.IgnoreDuplicates || options.UpdateExisting)
                        {
                            var checkSql = $@"
                                SELECT COUNT(*)
                                FROM {this.tableName}
                                WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
                                  AND IsDeleted = 0";

                            using var checkCmd = new SQLiteCommand(checkSql, connection);
                            checkCmd.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                            var exists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;

                            if (exists)
                            {
                                if (options.UpdateExisting)
                                {
                                    // Update existing entity with new version
                                    entity.Version = version;
                                    entity.LastWriteTime = DateTimeOffset.UtcNow;

                                    using var updateCmd = this.Mapper.CreateCommand(DbOperationType.Insert, entity.Id, entity, null);
                                    updateCmd.Connection = connection;
                                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                                    result.SuccessCount++;
                                }
                                else if (!options.IgnoreDuplicates)
                                {
                                    result.DuplicateCount++;
                                    result.Errors.Add($"Duplicate key found: {entity.Id}");
                                }
                                else
                                {
                                    result.DuplicateCount++;
                                }
                                processedCount++;
                                continue;
                            }
                        }

                        // Insert new entity
                        entity.Version = version;
                        entity.CreatedTime = DateTimeOffset.UtcNow;
                        entity.LastWriteTime = DateTimeOffset.UtcNow;
                        entity.IsDeleted = false;

                        using var insertCmd = this.Mapper.CreateCommand(DbOperationType.Insert, entity.Id, entity, null);
                        insertCmd.Connection = connection;
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);

                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Error importing entity {entity.Id}: {ex.Message}");
                    }

                    processedCount++;

                    // Report progress
                    if (progress != null && processedCount % 100 == 0)
                    {
                        var progressInfo = new BulkOperationProgress
                        {
                            ProcessedCount = processedCount,
                            TotalCount = totalCount,
                            ElapsedTime = DateTime.UtcNow - startTime,
                            CurrentOperation = $"Importing entities ({processedCount}/{totalCount})"
                        };
                        progress.Report(progressInfo);
                        Logger.BulkOperationProgress((int)progressInfo.PercentComplete, processedCount, totalCount);
                    }
                }
            }

                // Final progress report
                if (progress != null)
                {
                    progress.Report(new BulkOperationProgress
                    {
                        ProcessedCount = processedCount,
                        TotalCount = totalCount,
                        ElapsedTime = DateTime.UtcNow - startTime,
                        CurrentOperation = "Import completed"
                    });
                    
                    Logger.BulkOperationProgress(100, processedCount, totalCount);
                }

                result.Duration = DateTime.UtcNow - startTime;
                stopwatch.Stop();
                Logger.BulkOperationStop("Import", processedCount, stopwatch);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BulkOperationFailed("Import", stopwatch, ex);
                throw;
            }
        }

        public async Task<BulkExportResult<T>> BulkExportAsync(Expression<Func<T, bool>> predicate = null, BulkExportOptions options = null, IProgress<BulkOperationProgress> progress = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkExportOptions();
            var startTime = DateTime.UtcNow;
            var exportedEntities = new List<T>();

            var stopwatch = Stopwatch.StartNew();
            Logger.BulkOperationStart("Export", 0); // Count unknown at start
            
            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);

            // Build WHERE clause
            var whereClause = "1=1"; // Default condition
            var parameters = new Dictionary<string, object>();

            if (predicate != null)
            {
                var translator = new Mappings.SQLiteExpressionTranslator<T>(
                    this.Mapper.GetPropertyMappings(),
                    () => this.Mapper.GetPrimaryKeyColumn());
                var translationResult = translator.Translate(predicate);
                whereClause = translationResult.Sql;
                parameters = translationResult.Parameters;
            }

            // Add IsDeleted condition based on options
            if (!options.IncludeDeleted)
            {
                whereClause = $"({whereClause}) AND IsDeleted = 0";
            }

            // First, get total count for progress reporting
            long totalCount = 0;
            if (progress != null)
            {
                var countSql = $@"
                    WITH LatestVersions AS (
                        SELECT {this.Mapper.GetPrimaryKeyColumn()}, MAX(Version) as MaxVersion
                        FROM {this.tableName}
                        WHERE {whereClause}
                        GROUP BY {this.Mapper.GetPrimaryKeyColumn()}
                    )
                    SELECT COUNT(*) FROM LatestVersions";

                using var countCmd = new SQLiteCommand(countSql, connection);
                foreach (var param in parameters)
                {
                    countCmd.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }
                totalCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));
            }

            // Build the main export query - get latest version of each entity
            var sql = $@"
                WITH LatestVersions AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY {this.Mapper.GetPrimaryKeyColumn()} ORDER BY Version DESC) as rn
                    FROM {this.tableName}
                    WHERE {whereClause}
                )
                SELECT {string.Join(", ", this.Mapper.GetSelectColumns().Select(c => $"lv.{c}"))}
                FROM LatestVersions lv
                WHERE lv.rn = 1
                ORDER BY lv.{this.Mapper.GetPrimaryKeyColumn()}";

            using var command = new SQLiteCommand(sql, connection);

            // Add parameters
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
            }

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var processedCount = 0;
            var batch = new List<T>();

            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entity = this.Mapper.MapFromReader(reader);
                if (entity != null)
                {
                    batch.Add(entity);
                    processedCount++;

                    // Process in batches to manage memory
                    if (batch.Count >= options.BatchSize)
                    {
                        exportedEntities.AddRange(batch);
                        batch.Clear();

                        // Report progress
                        if (progress != null)
                        {
                            var progressInfo = new BulkOperationProgress
                            {
                                ProcessedCount = processedCount,
                                TotalCount = totalCount,
                                ElapsedTime = DateTime.UtcNow - startTime,
                                CurrentOperation = $"Exporting entities ({processedCount}/{totalCount})"
                            };
                            progress.Report(progressInfo);
                            Logger.BulkOperationProgress((int)progressInfo.PercentComplete, processedCount, totalCount);
                        }
                    }
                }
            }

            // Add remaining entities
            if (batch.Count > 0)
            {
                exportedEntities.AddRange(batch);
            }

            // Final progress report
            if (progress != null)
            {
                progress.Report(new BulkOperationProgress
                {
                    ProcessedCount = processedCount,
                    TotalCount = totalCount,
                    ElapsedTime = DateTime.UtcNow - startTime,
                    CurrentOperation = "Export completed"
                });
                
                Logger.BulkOperationProgress(100, processedCount, totalCount);
            }

            stopwatch.Stop();
            Logger.BulkOperationStop("Export", exportedEntities.Count, stopwatch);

            return new BulkExportResult<T>
            {
                ExportedEntities = exportedEntities,
                ExportedCount = exportedEntities.Count,
                Duration = DateTime.UtcNow - startTime
            };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.BulkOperationFailed("Export", stopwatch, ex);
                throw;
            }
        }

        #endregion

        public ITransactionScope<T, TKey> BeginTransaction(CancellationToken cancellationToken = default)
        {
            return new TransactionScope<T, TKey>(this.connectionString, this);
        }

        public async Task OptimizeStorageAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.OptimizeStorageStart();
            
            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync(cancellationToken);
                
                using var command = new SQLiteCommand("VACUUM;", connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
                
                stopwatch.Stop();
                Logger.OptimizeStorageStop(stopwatch);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.SqlExecuteFailed(ex);
                throw;
            }
        }

        public Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Estimates the size of an entity by serializing it.
        /// </summary>
        private long EstimateEntitySize(T entity)
        {
            if (entity == null) return 0;

            try
            {
                var serialized = this.Mapper.SerializeEntity(entity);
                return serialized?.Length ?? 0;
            }
            catch
            {
                // If serialization fails, return 0
                return 0;
            }
        }


        #region Command Creation Operations

        /// <summary>
        /// Creates a SELECT command for retrieving an entity.
        /// </summary>
        public SQLiteCommand CreateSelectCommand(TKey key)
        {
            // Create a dummy entity to use with the mapper
            var entity = Activator.CreateInstance<T>();
            entity.Id = key;

            // Use the mapper to create the command
            var command = this.Mapper.CreateCommand(DbOperationType.Select, key, entity, null);
            // Note: Connection will be assigned by caller
            return command;
        }

        /// <summary>
        /// Creates an INSERT command for adding a new entity.
        /// </summary>
        public SQLiteCommand CreateInsertCommand(T entity)
        {
            // Use the mapper to create the command
            return this.Mapper.CreateCommand(DbOperationType.Insert, entity.Id, entity, null);
        }

        /// <summary>
        /// Creates an UPDATE command for modifying an existing entity.
        /// </summary>
        public SQLiteCommand CreateUpdateCommand(T fromEntity, T toEntity)
        {
            // Use the mapper to create the command
            return this.Mapper.CreateCommand(DbOperationType.Update, fromEntity.Id, fromEntity, toEntity);
        }

        /// <summary>
        /// Creates a DELETE command for removing an entity.
        /// </summary>
        public SQLiteCommand CreateDeleteCommand(TKey key)
        {
            // Use the mapper to create the command
            return this.Mapper.CreateCommand(DbOperationType.Delete, key, null, null);
        }

        #endregion
    }
}