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
        private readonly string tableName;

        public SQLitePersistenceProvider(
            string connectionString,
            ISQLiteEntityMapper<T, TKey> mapper)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.tableName = this.mapper.GetTableName();
        }

        #region CRUD Operations - Translating Func delegates to SQL

        /// <summary>
        /// Implements Get = Func&lt;TKey, T&gt; by translating to parameterized SQL SELECT.
        /// Returns the latest version of the entity (highest version number).
        /// </summary>
        public async Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            // Translate Get = Func<TKey, T> to SQL
            // Order by Version DESC to get the latest version first
            // IMPORTANT: We do NOT filter by IsDeleted = 0 here because:
            // 1. We need to return the latest version regardless of deletion status
            // 2. The caller needs to know if an entity was deleted (by checking IsDeleted flag)
            // 3. Filtering would hide deleted entities and make it impossible to distinguish
            //    between "never existed" and "was deleted"
            var sql = $@"
                SELECT {this.mapper.GetSelectColumns()}
                FROM {this.tableName}
                WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                ORDER BY Version DESC
                LIMIT 1";

            T result = null;

            using var connection = new SQLiteConnection(this.connectionString);
            // Always pass cancellation token to OpenAsync for proper cancellation support
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
        /// Implements Create = Func&lt;T, T&gt; by translating to parameterized SQL INSERT.
        /// </summary>
        public async Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
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
        /// Implements Update = Func&lt;T, T&gt; by translating to parameterized SQL UPDATE.
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
                T oldValue = default(T);
                if (callerInfo != null)
                {
                    // Note: Here we DO filter by IsDeleted = 0 because we're verifying
                    // the entity exists and matches the expected version for update
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
                        historyCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(entity.Id));
                        historyCommand.Parameters.AddWithValue("@typeName", typeof(T).Name);
                        historyCommand.Parameters.AddWithValue("@version", newVersion);
                        historyCommand.Parameters.AddWithValue("@oldVersion", originalVersion);
                        historyCommand.Parameters.AddWithValue("@size", EstimateEntitySize(entity));
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

                return entity;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Implements Delete = Func&lt;TKey, bool&gt; by translating to SQL UPDATE (soft delete) or DELETE.
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
                        SELECT {this.mapper.GetSelectColumns()}
                        FROM {this.tableName}
                        WHERE {this.mapper.GetPrimaryKeyColumn()} = @key
                        ORDER BY Version DESC
                        LIMIT 1";

                    using var selectOldCommand = new SQLiteCommand(selectOldSql, connection, transaction);
                    selectOldCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));

                    using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                    if (await oldReader.ReadAsync(cancellationToken))
                    {
                        oldValue = this.mapper.MapFromReader(oldReader);
                        version = oldValue.Version;

                        // Only proceed with audit if the entity wasn't already deleted
                        if (oldValue.IsDeleted)
                        {
                            oldValue = default(T);
                        }
                    }
                }

                using var command = new SQLiteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));

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
                        historyCommand.Parameters.AddWithValue("@key", this.mapper.SerializeKey(key));
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

                return rowsAffected > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public Task<IEnumerable<T>> CreateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> UpdateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> DeleteBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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

        public ITransactionScope BeginTransaction(CancellationToken cancellationToken = default)
        {
            return new TransactionScope(this.connectionString);
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

        /// <summary>
        /// Estimates the size of an entity by serializing it.
        /// </summary>
        private long EstimateEntitySize(T entity)
        {
            if (entity == null) return 0;

            try
            {
                var serialized = this.mapper.SerializeEntity(entity);
                return serialized?.Length ?? 0;
            }
            catch
            {
                // If serialization fails, return 0
                return 0;
            }
        }

        #endregion

        // Additional methods implementation continues...
    }
}