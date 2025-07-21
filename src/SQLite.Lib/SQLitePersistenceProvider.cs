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
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Serialization;

    /// <summary>
    /// SQLite implementation of IPersistenceProvider that translates CRUD operations to SQL.
    /// </summary>
    public class SQLitePersistenceProvider<T, TKey> : IPersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly string connectionString;
        private readonly ISQLiteEntityMapper<T, TKey> mapper;
        private readonly ISerializer<T> serializer;
        private readonly string tableName;

        public SQLitePersistenceProvider(
            string connectionString,
            ISQLiteEntityMapper<T, TKey> mapper,
            ISerializer<T> serializer = null)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.serializer = serializer ?? SerializerResolver.CreateSerializer<T>();
            this.tableName = this.mapper.GetTableName();
        }


        #region CRUD Operations - Translating Func delegates to SQL

        /// <summary>
        /// Implements Get by translating to parameterized SQL SELECT.
        /// Returns the latest version of the entity (highest version number).
        /// </summary>
        public async Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            // Translate Get = Func<TKey, T> to SQL
            // Order by Version DESC to get the latest version first
            var sql = $@"
                SELECT {this.mapper.GetSelectColumns()}
                FROM {this.tableName}
                WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                ORDER BY Version DESC
                LIMIT 1";

            T result = null;

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SQLiteCommand(sql, connection);
            // Add parameter for type safety and SQL injection prevention
            command.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));

            long? version = null;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var foundEntity = this.mapper.MapFromReader(reader);

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
                    historyCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));
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

        /// <summary>
        /// Implements Create by translating to parameterized SQL INSERT.
        /// </summary>
        public async Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                // Step 1: Check if entity already exists and is not deleted
                var checkExistsSql = $@"
                    SELECT {this.mapper.GetSelectColumns()}
                    FROM {this.tableName}
                    WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                    ORDER BY Version DESC
                    LIMIT 1";

                using var checkCommand = new SQLiteCommand(checkExistsSql, connection, transaction);
                checkCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));

                using var checkReader = await checkCommand.ExecuteReaderAsync(cancellationToken);
                if (await checkReader.ReadAsync(cancellationToken))
                {
                    var existingEntity = this.mapper.MapFromReader(checkReader);
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
                var columns = this.mapper.GetInsertColumns();
                var parameters = columns.Select(c => $"@{c}").ToList();

                // Step 2: Insert into Version table to get next version
                var insertVersionSql = "INSERT INTO Version DEFAULT VALUES; SELECT last_insert_rowid();";

                using var versionCommand = new SQLiteCommand(insertVersionSql, connection, transaction);
                var version = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                entity.Version = version;

                // Step 3: Insert entity with the version from step 2
                var insertEntitySql = $@"
                    INSERT INTO {this.tableName} ({string.Join(", ", columns)})
                    VALUES ({string.Join(", ", parameters)});";

                using var insertCommand = new SQLiteCommand(insertEntitySql, connection, transaction);
                this.mapper.AddParameters(insertCommand, entity);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);

                // Step 4: Retrieve the inserted entity
                var selectSql = $@"
                    SELECT {this.mapper.GetSelectColumns()}
                    FROM {this.tableName}
                    WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                      AND Version = @version;";

                using var selectCommand = new SQLiteCommand(selectSql, connection, transaction);
                selectCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));
                selectCommand.Parameters.AddWithValue("@version", version);

                using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var result = this.mapper.MapFromReader(reader);
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
                            historyCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));
                            historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                            historyCommand.Parameters.AddWithValue("@version", version);
                            historyCommand.Parameters.AddWithValue("@size", entity.EstimateEntitySize());
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

                throw new InvalidOperationException("Failed to retrieve created entity");
            }
            catch
            {
                // Transaction rollback will undo both Version and entity inserts
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Implements Update by translating to parameterized SQL UPDATE.
        /// </summary>
        public async Task<T> UpdateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            // Store original version for optimistic concurrency check
            var originalVersion = entity.Version;

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                // Step 1: Get the old value for history tracking
                T oldValue = null;
                if (callerInfo != null)
                {
                    var selectOldSql = $@"
                        SELECT {this.mapper.GetSelectColumns()}
                        FROM {this.tableName}
                        WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                          AND Version = @originalVersion
                          AND IsDeleted = 0";

                    using var selectOldCommand = new SQLiteCommand(selectOldSql, connection, transaction);
                    selectOldCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));
                    selectOldCommand.Parameters.AddWithValue("@originalVersion", originalVersion);

                    using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                    if (await oldReader.ReadAsync(cancellationToken))
                    {
                        oldValue = this.mapper.MapFromReader(oldReader);
                    }
                }

                // Step 2: Insert into Version table to get next version
                var insertVersionSql = "INSERT INTO Version (Timestamp) VALUES (datetime('now')); SELECT last_insert_rowid();";

                using var versionCommand = new SQLiteCommand(insertVersionSql, connection, transaction);
                var newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));

                // Update tracking fields
                entity.LastWriteTime = DateTimeOffset.UtcNow;
                entity.Version = newVersion;

                // Build SET clause with all updatable columns
                var updateColumns = this.mapper.GetUpdateColumns()
                    .Select(c => $"{c} = @{c}")
                    .ToList();

                // Step 2: Update entity with new version and optimistic concurrency check
                var updateSql = $@"
                    UPDATE {this.tableName}
                    SET {string.Join(", ", updateColumns)}
                    WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                      AND Version = @originalVersion
                      AND IsDeleted = 0;

                    SELECT changes();";

                using var updateCommand = new SQLiteCommand(updateSql, connection, transaction);

                // Add all parameters including concurrency check
                this.mapper.AddParameters(updateCommand, entity);
                updateCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));
                updateCommand.Parameters.AddWithValue("@originalVersion", originalVersion);

                var rowsAffected = Convert.ToInt32(await updateCommand.ExecuteScalarAsync(cancellationToken));

                if (rowsAffected == 0)
                {
                    throw new ConcurrencyException(
                        $"Entity with key '{entity.Id}' has been modified by another process.");
                }

                await transaction.CommitAsync();

                // Populate CacheUpdateHistory after commit
                if (callerInfo != null)
                {
                    try
                    {
                        var historyInsertSql = @"
                            INSERT INTO CacheUpdateHistory (
                                CacheKey, Version, UpdateTime, UpdateType,
                                CallerMemberName, CallerFilePath, CallerLineNumber,
                                OldValue, NewValue
                            ) VALUES (
                                @key, @version, datetime('now'), 'Update',
                                @memberName, @filePath, @lineNumber,
                                @oldValue, @newValue
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync();

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        historyCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));
                        historyCommand.Parameters.AddWithValue("@version", newVersion);
                        historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);
                        historyCommand.Parameters.AddWithValue("@oldValue", oldValue != null ? JsonSerializer.Serialize(oldValue) : (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@newValue", JsonSerializer.Serialize(entity));

                        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                return entity;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Implements Delete = Func<TKey, bool> by translating to SQL UPDATE (soft delete) or DELETE.
        /// </summary>
        public async Task<bool> DeleteAsync(TKey key, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default)
        {
            string sql;

            if (hardDelete)
            {
                // Translate to SQL DELETE for hard delete
                sql = $@"
                    DELETE FROM {this.tableName}
                    WHERE {this.mapper.GetPrimaryKeyColumn()} = @key;

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
                    WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                      AND IsDeleted = 0;

                    SELECT changes();";
            }

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Get the old value for history tracking
                T oldValue = default(T);
                long version = 0;

                if (callerInfo != null)
                {
                    var selectOldSql = $@"
                        SELECT {this.mapper.GetSelectColumns()}
                        FROM {this.tableName}
                        WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                          AND IsDeleted = 0
                        ORDER BY Version DESC
                        LIMIT 1";

                    using var selectOldCommand = new SQLiteCommand(selectOldSql, connection, transaction);
                    selectOldCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));

                    using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                    if (await oldReader.ReadAsync(cancellationToken))
                    {
                        oldValue = this.mapper.MapFromReader(oldReader);
                        version = oldValue.Version;
                    }
                }

                using var command = new SQLiteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));

                if (!hardDelete)
                {
                    command.Parameters.AddWithValue("@lastWriteTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }

                var rowsAffected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                await transaction.CommitAsync();

                // Populate CacheUpdateHistory after commit
                if (rowsAffected > 0 && callerInfo != null)
                {
                    try
                    {
                        var historyInsertSql = @"
                            INSERT INTO CacheUpdateHistory (
                                CacheKey, Version, UpdateTime, UpdateType,
                                CallerMemberName, CallerFilePath, CallerLineNumber,
                                OldValue, NewValue
                            ) VALUES (
                                @key, @version, datetime('now'), @updateType,
                                @memberName, @filePath, @lineNumber,
                                @oldValue, NULL
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync();

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        historyCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));
                        historyCommand.Parameters.AddWithValue("@version", version);
                        historyCommand.Parameters.AddWithValue("@updateType", hardDelete ? "HardDelete" : "SoftDelete");
                        historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);
                        historyCommand.Parameters.AddWithValue("@oldValue", oldValue != null ? JsonSerializer.Serialize(oldValue) : (object)DBNull.Value);

                        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                return rowsAffected > 0;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Implements GetBatch to retrieve multiple entities by their keys.
        /// Returns only the latest version of each entity.
        /// </summary>
        public async Task<IEnumerable<T>> GetBatchAsync(
            IEnumerable<TKey> keys,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            var keyList = keys.ToList();
            if (!keyList.Any())
                return Enumerable.Empty<T>();

            // Serialize keys for SQL IN clause
            var serializedKeys = keyList.Select(k => this.mapper.SerializeKey(k)).ToList();
            var parameters = serializedKeys.Select((_, i) => $"@key{i}").ToList();

            // Use subquery to get only the latest version per key
            var sql = $@"
                SELECT {this.mapper.GetSelectColumns()}
                FROM {this.tableName} t1
                WHERE t1.{this.mapper.GetPrimaryKeyColumn()} IN ({string.Join(", ", parameters)})
                  AND t1.IsDeleted = 0
                  AND t1.Version = (
                      SELECT MAX(t2.Version)
                      FROM {this.tableName} t2
                      WHERE t2.{this.mapper.GetPrimaryKeyColumn()} = t1.{this.mapper.GetPrimaryKeyColumn()}
                        AND t2.IsDeleted = 0
                  )";

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();

            using var command = new SQLiteCommand(sql, connection);

            // Add parameters
            for (int i = 0; i < serializedKeys.Count; i++)
            {
                command.Parameters.AddWithValue($"@key{i}", serializedKeys[i]);
            }

            using var transaction = connection.BeginTransaction();
            try
            {
                command.Transaction = transaction;

                var results = new List<T>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(this.mapper.MapFromReader(reader));
                }

                await transaction.CommitAsync();

                // Populate CacheAccessHistory after commit - single record for batch
                if (callerInfo != null && results.Any())
                {
                    try
                    {
                        // Get max version from results
                        var maxVersion = results.Max(r => r.Version);

                        var historyInsertSql = @"
                            INSERT INTO CacheAccessHistory (
                                CacheKey, Version, AccessTime, AccessType,
                                CallerMemberName, CallerFilePath, CallerLineNumber
                            ) VALUES (
                                @key, @version, datetime('now'), 'GetBatch',
                                @memberName, @filePath, @lineNumber
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync();

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        // Use typename for batch operations
                        historyCommand.Parameters.AddWithValue("@key", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@version", maxVersion);
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

                return results;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<IEnumerable<T>> UpdateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> DeleteBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Batch Operations - Translating collections to bulk SQL

        /// <summary>
        /// Implements batch create by translating to multi-row INSERT.
        /// </summary>
        public async Task<IEnumerable<T>> CreateBatchAsync(
            IEnumerable<T> entities,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
                return Enumerable.Empty<T>();

            var columns = this.mapper.GetInsertColumns();
            var createdEntities = new List<T>();

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Prepare version insert command
                var insertVersionSql = "INSERT INTO Version (Timestamp) VALUES (datetime('now')); SELECT last_insert_rowid();";
                using var versionCommand = new SQLiteCommand(insertVersionSql, connection, transaction);

                // Prepare entity insert command
                var insertEntitySql = $@"
                    INSERT INTO {this.tableName} ({string.Join(", ", columns)})
                    VALUES ({string.Join(", ", columns.Select(c => $"@{c}"))})";

                using var entityCommand = new SQLiteCommand(insertEntitySql, connection, transaction);
                await entityCommand.PrepareAsync();

                foreach (var entity in entityList)
                {
                    // Step 1: Insert into Version table to get next version
                    var version = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));

                    // Set tracking fields
                    entity.CreatedTime = DateTimeOffset.UtcNow;
                    entity.LastWriteTime = entity.CreatedTime;
                    entity.Version = version;

                    // Step 2: Insert entity with the new version
                    entityCommand.Parameters.Clear();
                    this.mapper.AddParameters(entityCommand, entity);

                    await entityCommand.ExecuteNonQueryAsync(cancellationToken);
                    createdEntities.Add(entity);
                }

                await transaction.CommitAsync();

                // Populate CacheUpdateHistory after commit - single record for batch
                if (callerInfo != null && createdEntities.Any())
                {
                    try
                    {
                        // Get max version from created entities
                        var maxVersion = createdEntities.Max(e => e.Version);

                        var historyInsertSql = @"
                            INSERT INTO CacheUpdateHistory (
                                CacheKey, Version, UpdateTime, UpdateType,
                                CallerMemberName, CallerFilePath, CallerLineNumber,
                                OldValue, NewValue
                            ) VALUES (
                                @key, @version, datetime('now'), 'CreateBatch',
                                @memberName, @filePath, @lineNumber,
                                NULL, @newValue
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync();

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        // Use typename for batch operations
                        historyCommand.Parameters.AddWithValue("@key", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@version", maxVersion);
                        historyCommand.Parameters.AddWithValue("@memberName", callerInfo.CallerMemberName ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@filePath", callerInfo.CallerFilePath ?? (object)DBNull.Value);
                        historyCommand.Parameters.AddWithValue("@lineNumber", callerInfo.CallerLineNumber);
                        historyCommand.Parameters.AddWithValue("@newValue", $"Batch created {createdEntities.Count} entities");

                        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                return createdEntities;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Query Operations - Translating Expression<Func<T, bool>> to SQL

        /// <summary>
        /// Translates LINQ expressions to SQL WHERE clauses.
        /// </summary>
        public async Task<IEnumerable<T>> QueryAsync(
            Expression<Func<T, bool>> predicate,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            // Use expression visitor to translate LINQ to SQL
            var whereClause = new SQLiteExpressionTranslator<T>().Translate(predicate);

            // Use subquery to get only the latest version per key
            var sql = $@"
                SELECT {this.mapper.GetSelectColumns()}
                FROM {this.tableName} t1
                WHERE t1.IsDeleted = 0
                  AND ({whereClause.Sql})
                  AND t1.Version = (
                      SELECT MAX(t2.Version)
                      FROM {this.tableName} t2
                      WHERE t2.{this.mapper.GetPrimaryKeyColumn()} = t1.{this.mapper.GetPrimaryKeyColumn()}
                        AND t2.IsDeleted = 0
                  )";

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();

            using var command = new SQLiteCommand(sql, connection);

            // Add parameters extracted from expression
            foreach (var param in whereClause.Parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }

            using var transaction = connection.BeginTransaction();
            try
            {
                command.Transaction = transaction;

                var results = new List<T>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(this.mapper.MapFromReader(reader));
                }

                await transaction.CommitAsync();

                // Populate CacheAccessHistory after commit - single record for query
                if (callerInfo != null && results.Any())
                {
                    try
                    {
                        // Get max version from results
                        var maxVersion = results.Max(r => r.Version);

                        var historyInsertSql = @"
                            INSERT INTO CacheAccessHistory (
                                CacheKey, Version, AccessTime, AccessType,
                                CallerMemberName, CallerFilePath, CallerLineNumber
                            ) VALUES (
                                @key, @version, datetime('now'), 'Query',
                                @memberName, @filePath, @lineNumber
                            )";

                        using var historyConnection = new SQLiteConnection(this.connectionString);
                        await historyConnection.OpenAsync();

                        using var historyCommand = new SQLiteCommand(historyInsertSql, historyConnection);
                        // Use typename for batch operations
                        historyCommand.Parameters.AddWithValue("@key", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@version", maxVersion);
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

                return results;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<PagedResult<T>> QueryPagedAsync(Expression<Func<T, bool>> predicate, int pageSize, int pageNumber, Expression<Func<T, IComparable>> orderBy = null, bool ascending = true, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<long> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<BulkImportResult> BulkImportAsync(IEnumerable<T> entities, BulkImportOptions options = null, IProgress<BulkOperationProgress> progress = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<BulkExportResult<T>> BulkExportAsync(Expression<Func<T, bool>> predicate = null, BulkExportOptions options = null, IProgress<BulkOperationProgress> progress = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task OptimizeStorageAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}