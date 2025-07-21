# Appendix E: SQLitePersistenceProvider Implementation

## E.1 CRUD Operations Translation

This appendix shows how the SQLitePersistenceProvider translates generic CRUD operations defined as `Func<TKey, T>`, `Func<T, T>`, etc., into parameterized SQL statements.

### SQLitePersistenceProvider Core Implementation

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AzureStack.Services.Update.Common.Persistence;
using Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.SQLite
{
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
        /// Implements Get = Func<TKey, T> by translating to parameterized SQL SELECT.
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
        /// Implements Create = Func<T, T> by translating to parameterized SQL INSERT.
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
        /// Implements Update = Func<T, T> by translating to parameterized SQL UPDATE.
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

        /// <summary>
        /// Estimates the size of an entity by serializing it.
        /// </summary>
        private long EstimateEntitySize(T entity)
        {
            if (entity == null) return 0;
            
            try
            {
                var serialized = this.serializer.Serialize(entity);
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
```

## E.2 Attribute-Based Entity Mapping

### Mapping Attributes

```csharp
using System;
using System.Data;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Mapping
{
    /// <summary>
    /// Specifies the database table name and schema for an entity class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the schema name. Default is "dbo".
        /// </summary>
        public string Schema { get; set; } = "dbo";

        public TableAttribute(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// Maps a property to a database column with specific settings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the column name. If not specified, property name is used.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the SQL data type.
        /// </summary>
        public SqlDbType? SqlType { get; set; }

        /// <summary>
        /// Gets or sets the size/length of the column. -1 indicates MAX.
        /// </summary>
        public int Size { get; set; } = 0;

        /// <summary>
        /// Gets or sets the precision for numeric columns.
        /// </summary>
        public int Precision { get; set; } = 0;

        /// <summary>
        /// Gets or sets the scale for numeric columns.
        /// </summary>
        public int Scale { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether the column allows NULL values.
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// Gets or sets the default value for the column.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the name of the default constraint.
        /// </summary>
        public string DefaultConstraintName { get; set; }

        /// <summary>
        /// Gets or sets the column order in the table.
        /// </summary>
        public int Order { get; set; } = -1;

        public ColumnAttribute()
        {
        }

        public ColumnAttribute(string name)
        {
            this.Name = name;
        }

        public ColumnAttribute(string name, SqlDbType sqlType) : this(name)
        {
            this.SqlType = sqlType;
        }
    }

    /// <summary>
    /// Marks a property as the primary key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class PrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether the primary key is auto-incremented.
        /// </summary>
        public bool IsAutoIncrement { get; set; }

        /// <summary>
        /// Gets or sets whether this is part of a composite key.
        /// </summary>
        public bool IsComposite { get; set; }

        /// <summary>
        /// Gets or sets the order in a composite key.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets the sequence name for key generation.
        /// </summary>
        public string SequenceName { get; set; }
    }

    /// <summary>
    /// Marks a property for database indexing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class IndexAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the index name. If not specified, a default name is generated.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the column order in a composite index.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether this is a unique index.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets whether this is a clustered index.
        /// </summary>
        public bool IsClustered { get; set; }

        /// <summary>
        /// Gets or sets whether this column is included (not part of key).
        /// </summary>
        public bool IsIncluded { get; set; }

        /// <summary>
        /// Gets or sets the filter expression for a filtered index.
        /// </summary>
        public string Filter { get; set; }

        public IndexAttribute()
        {
        }

        public IndexAttribute(string name)
        {
            this.Name = name;
        }
    }

    /// <summary>
    /// Defines a foreign key relationship.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets the referenced table name.
        /// </summary>
        public string ReferencedTable { get; }

        /// <summary>
        /// Gets or sets the referenced column name. Default is "Id".
        /// </summary>
        public string ReferencedColumn { get; set; } = "Id";

        /// <summary>
        /// Gets or sets the constraint name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ON DELETE behavior.
        /// </summary>
        public string OnDelete { get; set; } = "NO ACTION";

        /// <summary>
        /// Gets or sets the ON UPDATE behavior.
        /// </summary>
        public string OnUpdate { get; set; } = "NO ACTION";

        public ForeignKeyAttribute(string referencedTable)
        {
            this.ReferencedTable = referencedTable ?? throw new ArgumentNullException(nameof(referencedTable));
        }
    }

    /// <summary>
    /// Marks a property as a computed column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ComputedAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the computation expression.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Gets or sets whether the computed value is persisted.
        /// </summary>
        public bool IsPersisted { get; set; }

        public ComputedAttribute()
        {
        }

        public ComputedAttribute(string expression)
        {
            this.Expression = expression;
        }
    }

    /// <summary>
    /// Marks a property as an audit field with automatic management.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AuditFieldAttribute : Attribute
    {
        /// <summary>
        /// Gets the type of audit field.
        /// </summary>
        public AuditFieldType FieldType { get; }

        public AuditFieldAttribute(AuditFieldType fieldType)
        {
            this.FieldType = fieldType;
        }
    }

    /// <summary>
    /// Types of audit fields.
    /// </summary>
    public enum AuditFieldType
    {
        CreatedTime,
        CreatedBy,
        LastWriteTime,
        LastWriteBy,
        Version,
        IsDeleted
    }

    /// <summary>
    /// Excludes a property from database mapping.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NotMappedAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies a unique constraint on a column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UniqueAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the constraint name.
        /// </summary>
        public string Name { get; set; }

        public UniqueAttribute()
        {
        }

        public UniqueAttribute(string name)
        {
            this.Name = name;
        }
    }

    /// <summary>
    /// Specifies a check constraint on a column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class CheckAttribute : Attribute
    {
        /// <summary>
        /// Gets the check constraint expression.
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// Gets or sets the constraint name.
        /// </summary>
        public string Name { get; set; }

        public CheckAttribute(string expression)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }

    /// <summary>
    /// Specifies custom serialization for a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class JsonConverterAttribute : Attribute
    {
        /// <summary>
        /// Gets the converter type.
        /// </summary>
        public Type ConverterType { get; }

        public JsonConverterAttribute(Type converterType)
        {
            this.ConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
        }
    }

    /// <summary>
    /// Specifies validation rules for a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ValidationAttribute : Attribute
    {
        /// <summary>
        /// Gets the validation rule name.
        /// </summary>
        public string Rule { get; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the validation rule.
        /// </summary>
        public object[] Parameters { get; set; }

        public ValidationAttribute(string rule)
        {
            this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }
    }

    /// <summary>
    /// Specifies that a property should be encrypted when stored.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EncryptedAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the encryption method.
        /// </summary>
        public string Method { get; set; } = "AES256";

        /// <summary>
        /// Gets or sets the key name for encryption.
        /// </summary>
        public string KeyName { get; set; }
    }

    /// <summary>
    /// Specifies database-specific settings for a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class DatabaseSpecificAttribute : Attribute
    {
        /// <summary>
        /// Gets the database provider name (e.g., "SQLite", "SqlServer").
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Gets or sets provider-specific SQL type.
        /// </summary>
        public string SqlType { get; set; }

        /// <summary>
        /// Gets or sets provider-specific settings as key-value pairs.
        /// </summary>
        public string Settings { get; set; }

        public DatabaseSpecificAttribute(string provider)
        {
            this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
    }
}
```

### BaseEntityMapper Implementation

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Mapping
{
    /// <summary>
    /// Base mapper that uses reflection and attributes to create mappings between C# properties and database columns.
    /// </summary>
    /// <typeparam name="T">The entity type to map</typeparam>
    public class BaseEntityMapper<T> where T : class, new()
    {
        private readonly Type entityType;
        private readonly string tableName;
        private readonly string schemaName;
        private readonly Dictionary<PropertyInfo, PropertyMapping> propertyMappings;
        private readonly List<IndexDefinition> indexes;
        private readonly List<ForeignKeyDefinition> foreignKeys;
        private readonly PropertyInfo primaryKeyProperty;
        private readonly bool hasCompositeKey;
        private readonly List<PropertyInfo> compositeKeyProperties;

        public BaseEntityMapper()
        {
            this.entityType = typeof(T);
            this.propertyMappings = new Dictionary<PropertyInfo, PropertyMapping>();
            this.indexes = new List<IndexDefinition>();
            this.foreignKeys = new List<ForeignKeyDefinition>();
            this.compositeKeyProperties = new List<PropertyInfo>();

            // Extract table information
            var tableAttr = this.entityType.GetCustomAttribute<TableAttribute>();
            this.tableName = tableAttr?.Name ?? this.entityType.Name;
            this.schemaName = tableAttr?.Schema ?? "dbo";

            // Build property mappings
            this.BuildPropertyMappings();

            // Validate primary key
            if (this.primaryKeyProperty == null && !this.hasCompositeKey)
            {
                throw new InvalidOperationException(
                    $"Entity type {this.entityType.Name} must have at least one property marked with [PrimaryKey] or properties named 'Id' or 'Key'");
            }
        }

        /// <summary>
        /// Gets the fully qualified table name including schema.
        /// </summary>
        public string GetFullTableName() => $"{this.schemaName}.{this.tableName}";

        /// <summary>
        /// Gets the table name without schema.
        /// </summary>
        public string GetTableName() => this.tableName;

        /// <summary>
        /// Gets all property mappings.
        /// </summary>
        public IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetPropertyMappings() => this.propertyMappings;

        /// <summary>
        /// Gets the primary key property mapping.
        /// </summary>
        public PropertyMapping GetPrimaryKeyMapping()
        {
            if (this.hasCompositeKey)
            {
                throw new InvalidOperationException("Entity has composite key. Use GetCompositeKeyMappings() instead.");
            }
            return this.propertyMappings[this.primaryKeyProperty];
        }

        /// <summary>
        /// Gets composite key property mappings if the entity has a composite primary key.
        /// </summary>
        public IEnumerable<PropertyMapping> GetCompositeKeyMappings()
        {
            if (!this.hasCompositeKey)
            {
                throw new InvalidOperationException("Entity has single primary key. Use GetPrimaryKeyMapping() instead.");
            }
            return this.compositeKeyProperties.Select(p => this.propertyMappings[p]);
        }

        /// <summary>
        /// Generates CREATE TABLE SQL statement for the entity.
        /// </summary>
        public string GenerateCreateTableSql(bool includeIfNotExists = true)
        {
            var sql = new StringBuilder();
            
            if (includeIfNotExists)
            {
                sql.AppendLine($"CREATE TABLE IF NOT EXISTS {this.GetFullTableName()} (");
            }
            else
            {
                sql.AppendLine($"CREATE TABLE {this.GetFullTableName()} (");
            }

            // Add column definitions
            var columnDefinitions = new List<string>();
            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                columnDefinitions.Add(this.GenerateColumnDefinition(mapping));
            }

            // Add primary key constraint
            if (this.hasCompositeKey)
            {
                var keyColumns = string.Join(", ", this.compositeKeyProperties
                    .Select(p => this.propertyMappings[p].ColumnName));
                columnDefinitions.Add($"PRIMARY KEY ({keyColumns})");
            }

            // Add foreign key constraints
            foreach (var fk in this.foreignKeys)
            {
                columnDefinitions.Add(this.GenerateForeignKeyConstraint(fk));
            }

            sql.AppendLine(string.Join(",\n", columnDefinitions.Select(d => $"    {d}")));
            sql.AppendLine(");");

            return sql.ToString();
        }

        /// <summary>
        /// Generates CREATE INDEX SQL statements for the entity.
        /// </summary>
        public IEnumerable<string> GenerateCreateIndexSql()
        {
            var indexSql = new List<string>();

            foreach (var index in this.indexes)
            {
                var sql = new StringBuilder();
                sql.Append("CREATE ");
                
                if (index.IsUnique)
                    sql.Append("UNIQUE ");
                    
                sql.Append($"INDEX IF NOT EXISTS {index.Name} ");
                sql.Append($"ON {this.GetFullTableName()} (");
                sql.Append(string.Join(", ", index.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName)));
                sql.Append(");");

                indexSql.Add(sql.ToString());
            }

            return indexSql;
        }

        private void BuildPropertyMappings()
        {
            var properties = this.entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var indexGroups = new Dictionary<string, List<IndexColumn>>();

            foreach (var property in properties)
            {
                // Check if property should be excluded
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                {
                    continue;
                }

                // Create property mapping
                var mapping = this.CreatePropertyMapping(property);
                this.propertyMappings[property] = mapping;

                // Check if this is a primary key
                var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
                if (pkAttr != null)
                {
                    if (pkAttr.IsComposite)
                    {
                        this.hasCompositeKey = true;
                        this.compositeKeyProperties.Add(property);
                    }
                    else
                    {
                        this.primaryKeyProperty = property;
                    }
                }
                else if (this.primaryKeyProperty == null && !this.hasCompositeKey && 
                         (property.Name == "Id" || property.Name == "Key"))
                {
                    // Convention-based primary key
                    this.primaryKeyProperty = property;
                    mapping.IsPrimaryKey = true;
                }

                // Process indexes
                var indexAttrs = property.GetCustomAttributes<IndexAttribute>();
                foreach (var indexAttr in indexAttrs)
                {
                    var indexName = indexAttr.Name ?? $"IX_{this.tableName}_{mapping.ColumnName}";
                    
                    if (!indexGroups.ContainsKey(indexName))
                    {
                        indexGroups[indexName] = new List<IndexColumn>();
                    }

                    indexGroups[indexName].Add(new IndexColumn
                    {
                        ColumnName = mapping.ColumnName,
                        Order = indexAttr.Order,
                        IsIncluded = indexAttr.IsIncluded
                    });
                }

                // Process foreign keys
                var fkAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                if (fkAttr != null)
                {
                    this.foreignKeys.Add(new ForeignKeyDefinition
                    {
                        ConstraintName = fkAttr.Name ?? $"FK_{this.tableName}_{property.Name}",
                        ColumnName = mapping.ColumnName,
                        ReferencedTable = fkAttr.ReferencedTable,
                        ReferencedColumn = fkAttr.ReferencedColumn ?? "Id",
                        OnDelete = fkAttr.OnDelete,
                        OnUpdate = fkAttr.OnUpdate
                    });
                }
            }

            // Build index definitions from grouped columns
            foreach (var group in indexGroups)
            {
                var firstColumn = group.Value.First();
                var firstIndexAttr = properties
                    .SelectMany(p => p.GetCustomAttributes<IndexAttribute>()
                        .Where(a => (a.Name ?? $"IX_{this.tableName}_{this.propertyMappings[p].ColumnName}") == group.Key))
                    .First();

                this.indexes.Add(new IndexDefinition
                {
                    Name = group.Key,
                    Columns = group.Value,
                    IsUnique = firstIndexAttr.IsUnique,
                    IsClustered = firstIndexAttr.IsClustered,
                    Filter = firstIndexAttr.Filter
                });
            }
        }

        private PropertyMapping CreatePropertyMapping(PropertyInfo property)
        {
            var mapping = new PropertyMapping
            {
                PropertyInfo = property,
                PropertyName = property.Name,
                PropertyType = property.PropertyType,
                IsNotMapped = false
            };

            // Get column attribute
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            
            // Column name
            mapping.ColumnName = columnAttr?.Name ?? property.Name;
            
            // Data type
            if (columnAttr?.SqlType != null)
            {
                mapping.SqlType = columnAttr.SqlType.Value;
                mapping.Size = columnAttr.Size;
                mapping.Precision = columnAttr.Precision;
                mapping.Scale = columnAttr.Scale;
            }
            else
            {
                // Infer SQL type from property type
                this.InferSqlType(property.PropertyType, mapping);
            }

            // Nullability
            mapping.IsNullable = columnAttr?.IsNullable ?? this.IsNullableType(property.PropertyType);
            
            // Default value
            mapping.DefaultValue = columnAttr?.DefaultValue;
            mapping.DefaultConstraintName = columnAttr?.DefaultConstraintName;
            
            // Check constraint
            var checkAttr = property.GetCustomAttribute<CheckAttribute>();
            if (checkAttr != null)
            {
                mapping.CheckConstraint = checkAttr.Expression;
                mapping.CheckConstraintName = checkAttr.Name ?? $"CK_{this.tableName}_{mapping.ColumnName}";
            }

            // Computed column
            var computedAttr = property.GetCustomAttribute<ComputedAttribute>();
            if (computedAttr != null)
            {
                mapping.IsComputed = true;
                mapping.ComputedExpression = computedAttr.Expression;
                mapping.IsPersisted = computedAttr.IsPersisted;
            }

            // Audit fields
            var auditAttr = property.GetCustomAttribute<AuditFieldAttribute>();
            if (auditAttr != null)
            {
                mapping.IsAuditField = true;
                mapping.AuditFieldType = auditAttr.FieldType;
            }

            // Primary key
            var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
            if (pkAttr != null)
            {
                mapping.IsPrimaryKey = true;
                mapping.IsAutoIncrement = pkAttr.IsAutoIncrement;
                mapping.SequenceName = pkAttr.SequenceName;
            }

            // Unique constraint
            var uniqueAttr = property.GetCustomAttribute<UniqueAttribute>();
            if (uniqueAttr != null)
            {
                mapping.IsUnique = true;
                mapping.UniqueConstraintName = uniqueAttr.Name ?? $"UQ_{this.tableName}_{mapping.ColumnName}";
            }

            return mapping;
        }

        private void InferSqlType(Type clrType, PropertyMapping mapping)
        {
            var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (underlyingType == typeof(string))
            {
                mapping.SqlType = SqlDbType.NVarChar;
                mapping.Size = 255; // Default size
            }
            else if (underlyingType == typeof(int))
            {
                mapping.SqlType = SqlDbType.Int;
            }
            else if (underlyingType == typeof(long))
            {
                mapping.SqlType = SqlDbType.BigInt;
            }
            else if (underlyingType == typeof(short))
            {
                mapping.SqlType = SqlDbType.SmallInt;
            }
            else if (underlyingType == typeof(byte))
            {
                mapping.SqlType = SqlDbType.TinyInt;
            }
            else if (underlyingType == typeof(bool))
            {
                mapping.SqlType = SqlDbType.Bit;
            }
            else if (underlyingType == typeof(decimal))
            {
                mapping.SqlType = SqlDbType.Decimal;
                mapping.Precision = 18;
                mapping.Scale = 2;
            }
            else if (underlyingType == typeof(double))
            {
                mapping.SqlType = SqlDbType.Float;
            }
            else if (underlyingType == typeof(float))
            {
                mapping.SqlType = SqlDbType.Real;
            }
            else if (underlyingType == typeof(DateTime))
            {
                mapping.SqlType = SqlDbType.DateTime2;
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                mapping.SqlType = SqlDbType.DateTimeOffset;
            }
            else if (underlyingType == typeof(TimeSpan))
            {
                mapping.SqlType = SqlDbType.Time;
            }
            else if (underlyingType == typeof(byte[]))
            {
                mapping.SqlType = SqlDbType.VarBinary;
                mapping.Size = -1; // MAX
            }
            else if (underlyingType == typeof(Guid))
            {
                mapping.SqlType = SqlDbType.UniqueIdentifier;
            }
            else if (underlyingType.IsEnum)
            {
                mapping.SqlType = SqlDbType.Int; // Store enums as integers by default
            }
            else
            {
                // Default to NVARCHAR for complex types (will be serialized)
                mapping.SqlType = SqlDbType.NVarChar;
                mapping.Size = -1; // MAX
            }
        }

        private bool IsNullableType(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private string GenerateColumnDefinition(PropertyMapping mapping)
        {
            var sql = new StringBuilder();
            sql.Append($"{mapping.ColumnName} ");

            // Data type
            sql.Append(this.GetSqlTypeString(mapping));

            // Primary key with auto-increment
            if (mapping.IsPrimaryKey && !this.hasCompositeKey)
            {
                sql.Append(" PRIMARY KEY");
                if (mapping.IsAutoIncrement)
                {
                    sql.Append(" AUTOINCREMENT");
                }
            }

            // Nullability
            if (!mapping.IsNullable && !mapping.IsPrimaryKey)
            {
                sql.Append(" NOT NULL");
            }

            // Unique constraint
            if (mapping.IsUnique && !mapping.IsPrimaryKey)
            {
                sql.Append(" UNIQUE");
            }

            // Default value
            if (mapping.DefaultValue != null)
            {
                sql.Append($" DEFAULT {this.FormatDefaultValue(mapping.DefaultValue, mapping.SqlType)}");
            }

            // Check constraint
            if (!string.IsNullOrEmpty(mapping.CheckConstraint))
            {
                sql.Append($" CHECK ({mapping.CheckConstraint})");
            }

            // Computed column
            if (mapping.IsComputed && !string.IsNullOrEmpty(mapping.ComputedExpression))
            {
                sql.Append($" AS ({mapping.ComputedExpression})");
                if (mapping.IsPersisted)
                {
                    sql.Append(" PERSISTED");
                }
            }

            return sql.ToString();
        }

        private string GetSqlTypeString(PropertyMapping mapping)
        {
            var typeStr = mapping.SqlType.ToString().ToUpper();

            // Handle special cases for SQLite
            switch (mapping.SqlType)
            {
                case SqlDbType.NVarChar:
                case SqlDbType.VarChar:
                case SqlDbType.NChar:
                case SqlDbType.Char:
                    typeStr = "TEXT";
                    break;
                case SqlDbType.Int:
                case SqlDbType.BigInt:
                case SqlDbType.SmallInt:
                case SqlDbType.TinyInt:
                case SqlDbType.Bit:
                    typeStr = "INTEGER";
                    break;
                case SqlDbType.Float:
                case SqlDbType.Real:
                case SqlDbType.Decimal:
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    typeStr = "REAL";
                    break;
                case SqlDbType.Binary:
                case SqlDbType.VarBinary:
                case SqlDbType.Image:
                    typeStr = "BLOB";
                    break;
                case SqlDbType.DateTime:
                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                case SqlDbType.Date:
                case SqlDbType.Time:
                    typeStr = "TEXT"; // SQLite stores dates as text
                    break;
                case SqlDbType.UniqueIdentifier:
                    typeStr = "TEXT";
                    break;
            }

            return typeStr;
        }

        private string FormatDefaultValue(object value, SqlDbType sqlType)
        {
            if (value == null)
                return "NULL";

            if (value is string strValue)
            {
                return $"'{strValue.Replace("'", "''")}'";
            }

            if (value is bool boolValue)
            {
                return boolValue ? "1" : "0";
            }

            if (value is DateTime || value is DateTimeOffset)
            {
                return "datetime('now')";
            }

            if (value.GetType().IsEnum)
            {
                return ((int)value).ToString();
            }

            return value.ToString();
        }

        private string GenerateForeignKeyConstraint(ForeignKeyDefinition fk)
        {
            var sql = new StringBuilder();
            sql.Append($"CONSTRAINT {fk.ConstraintName} ");
            sql.Append($"FOREIGN KEY ({fk.ColumnName}) ");
            sql.Append($"REFERENCES {fk.ReferencedTable}({fk.ReferencedColumn})");

            if (!string.IsNullOrEmpty(fk.OnDelete))
            {
                sql.Append($" ON DELETE {fk.OnDelete}");
            }

            if (!string.IsNullOrEmpty(fk.OnUpdate))
            {
                sql.Append($" ON UPDATE {fk.OnUpdate}");
            }

            return sql.ToString();
        }
    }

    /// <summary>
    /// Represents a mapping between a C# property and a database column.
    /// </summary>
    public class PropertyMapping
    {
        public PropertyInfo PropertyInfo { get; set; }
        public string PropertyName { get; set; }
        public Type PropertyType { get; set; }
        public string ColumnName { get; set; }
        public SqlDbType SqlType { get; set; }
        public int? Size { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public object DefaultValue { get; set; }
        public string DefaultConstraintName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsAutoIncrement { get; set; }
        public string SequenceName { get; set; }
        public bool IsUnique { get; set; }
        public string UniqueConstraintName { get; set; }
        public bool IsComputed { get; set; }
        public string ComputedExpression { get; set; }
        public bool IsPersisted { get; set; }
        public bool IsNotMapped { get; set; }
        public bool IsAuditField { get; set; }
        public AuditFieldType? AuditFieldType { get; set; }
        public string CheckConstraint { get; set; }
        public string CheckConstraintName { get; set; }
    }

    /// <summary>
    /// Represents an index definition.
    /// </summary>
    public class IndexDefinition
    {
        public string Name { get; set; }
        public List<IndexColumn> Columns { get; set; }
        public bool IsUnique { get; set; }
        public bool IsClustered { get; set; }
        public string Filter { get; set; }
    }

    /// <summary>
    /// Represents a column in an index.
    /// </summary>
    public class IndexColumn
    {
        public string ColumnName { get; set; }
        public int Order { get; set; }
        public bool IsIncluded { get; set; }
    }

    /// <summary>
    /// Represents a foreign key definition.
    /// </summary>
    public class ForeignKeyDefinition
    {
        public string ConstraintName { get; set; }
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
        public string OnDelete { get; set; }
        public string OnUpdate { get; set; }
    }
}
```

## E.3 CacheEntryMapper Implementation

### CacheEntryMapper for Generic CacheEntry<T>

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Mapping
{
    /// <summary>
    /// Specialized mapper for CacheEntry<T> entities that handles generic value serialization.
    /// </summary>
    /// <typeparam name="T">The type of value stored in the cache entry</typeparam>
    public class CacheEntryMapper<T> : BaseEntityMapper<CacheEntry<T>> where T : class
    {
        private readonly ISerializer<T> valueSerializer;
        private readonly string tableName;

        public CacheEntryMapper(ISerializer<T> valueSerializer = null, string tableName = "CacheEntry")
        {
            this.valueSerializer = valueSerializer ?? SerializerResolver.GetSerializer<T>();
            this.tableName = tableName;
        }

        /// <summary>
        /// Override to use custom table name (since CacheEntry<T> is generic).
        /// </summary>
        public override string GetTableName() => this.tableName;

        /// <summary>
        /// Override to use consistent full table name.
        /// </summary>
        public override string GetFullTableName() => this.tableName; // SQLite doesn't use schemas

        /// <summary>
        /// Override to generate CREATE TABLE SQL specific to CacheEntry storage.
        /// </summary>
        public override string GenerateCreateTableSql(bool includeIfNotExists = true)
        {
            var sql = new StringBuilder();
            
            if (includeIfNotExists)
            {
                sql.AppendLine($"CREATE TABLE IF NOT EXISTS {this.tableName} (");
            }
            else
            {
                sql.AppendLine($"CREATE TABLE {this.tableName} (");
            }

            // Define columns explicitly for CacheEntry
            sql.AppendLine("    CacheKey TEXT PRIMARY KEY NOT NULL,");
            sql.AppendLine("    TypeName TEXT NOT NULL,");
            sql.AppendLine("    Value BLOB NOT NULL,");
            sql.AppendLine("    Size INTEGER,");
            sql.AppendLine("    TTLSeconds INTEGER,");
            sql.AppendLine("    ExpirationTime INTEGER,");
            sql.AppendLine("    Tags TEXT,");
            sql.AppendLine("    Metadata TEXT,");
            sql.AppendLine("    Priority INTEGER DEFAULT 0,");
            sql.AppendLine("    AccessCount INTEGER DEFAULT 0,");
            sql.AppendLine("    LastAccessTime INTEGER,");
            sql.AppendLine("    CreatedTime INTEGER NOT NULL,");
            sql.AppendLine("    LastWriteTime INTEGER NOT NULL,");
            sql.AppendLine("    Version INTEGER NOT NULL,");
            sql.AppendLine("    IsDeleted INTEGER NOT NULL DEFAULT 0");
            sql.AppendLine(");");

            return sql.ToString();
        }

        /// <summary>
        /// Override to generate indexes specific to CacheEntry.
        /// </summary>
        public override IEnumerable<string> GenerateCreateIndexSql()
        {
            var indexes = new List<string>
            {
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_TypeName ON {this.tableName} (TypeName);",
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_ExpirationTime ON {this.tableName} (ExpirationTime);",
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_Priority_LastAccess ON {this.tableName} (Priority DESC, LastAccessTime DESC);",
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_Version ON {this.tableName} (Version DESC);",
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_IsDeleted ON {this.tableName} (IsDeleted);"
            };

            // Add indexes for tags if supported
            indexes.Add($"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_Tags ON {this.tableName} (Tags);");

            return indexes;
        }

        /// <summary>
        /// Override to handle CacheEntry<T> specific column mappings.
        /// </summary>
        public override List<string> GetSelectColumns()
        {
            return new List<string>
            {
                "CacheKey",
                "TypeName",
                "Value",
                "Size",
                "TTLSeconds",
                "ExpirationTime",
                "Tags",
                "Metadata",
                "Priority",
                "AccessCount",
                "LastAccessTime",
                "CreatedTime",
                "LastWriteTime",
                "Version",
                "IsDeleted"
            };
        }

        /// <summary>
        /// Override to handle CacheEntry<T> specific insert columns.
        /// </summary>
        public override List<string> GetInsertColumns()
        {
            return new List<string>
            {
                "CacheKey",
                "TypeName",
                "Value",
                "Size",
                "TTLSeconds",
                "ExpirationTime",
                "Tags",
                "Metadata",
                "Priority",
                "AccessCount",
                "LastAccessTime",
                "CreatedTime",
                "LastWriteTime",
                "Version",
                "IsDeleted"
            };
        }

        /// <summary>
        /// Override to handle CacheEntry<T> specific update columns.
        /// </summary>
        public override List<string> GetUpdateColumns()
        {
            return new List<string>
            {
                "TypeName",
                "Value",
                "Size",
                "TTLSeconds",
                "ExpirationTime",
                "Tags",
                "Metadata",
                "Priority",
                "AccessCount",
                "LastAccessTime",
                "LastWriteTime",
                "Version"
                // Note: CacheKey is primary key (not updated)
                // Note: CreatedTime is not updated
                // Note: IsDeleted is handled separately
            };
        }

        /// <summary>
        /// Override to add parameters with proper value serialization.
        /// </summary>
        public override void AddParameters(SQLiteCommand command, CacheEntry<T> entity)
        {
            // Serialize the generic value
            byte[] serializedValue = this.SerializeValue(entity.Value);
            
            command.Parameters.AddWithValue("@CacheKey", entity.Id ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TypeName", entity.TypeName ?? typeof(T).FullName);
            command.Parameters.AddWithValue("@Value", serializedValue);
            command.Parameters.AddWithValue("@Size", serializedValue?.Length ?? 0);
            command.Parameters.AddWithValue("@TTLSeconds", entity.TTLSeconds ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ExpirationTime", entity.ExpirationTime?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Tags", this.SerializeTags(entity.Tags));
            command.Parameters.AddWithValue("@Metadata", this.SerializeMetadata(entity.Metadata));
            command.Parameters.AddWithValue("@Priority", entity.Priority);
            command.Parameters.AddWithValue("@AccessCount", entity.AccessCount);
            command.Parameters.AddWithValue("@LastAccessTime", entity.LastAccessTime?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CreatedTime", entity.CreatedTime.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("@LastWriteTime", entity.LastWriteTime.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("@Version", entity.Version);
            command.Parameters.AddWithValue("@IsDeleted", entity.IsDeleted ? 1 : 0);
        }

        /// <summary>
        /// Override to map from reader with proper value deserialization.
        /// </summary>
        public override CacheEntry<T> MapFromReader(IDataReader reader)
        {
            var entity = new CacheEntry<T>();
            
            entity.Id = reader["CacheKey"] as string;
            entity.TypeName = reader["TypeName"] as string;
            
            // Deserialize the value
            var valueBytes = reader["Value"] as byte[];
            if (valueBytes != null)
            {
                entity.Value = this.DeserializeValue(valueBytes);
            }
            
            entity.Size = Convert.ToInt64(reader["Size"]);
            entity.TTLSeconds = reader["TTLSeconds"] == DBNull.Value ? null : Convert.ToInt32(reader["TTLSeconds"]);
            entity.ExpirationTime = reader["ExpirationTime"] == DBNull.Value ? null : DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reader["ExpirationTime"]));
            entity.Tags = this.DeserializeTags(reader["Tags"] as string);
            entity.Metadata = this.DeserializeMetadata(reader["Metadata"] as string);
            entity.Priority = Convert.ToInt32(reader["Priority"]);
            entity.AccessCount = Convert.ToInt64(reader["AccessCount"]);
            entity.LastAccessTime = reader["LastAccessTime"] == DBNull.Value ? null : DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reader["LastAccessTime"]));
            entity.CreatedTime = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reader["CreatedTime"]));
            entity.LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reader["LastWriteTime"]));
            entity.Version = Convert.ToInt64(reader["Version"]);
            entity.IsDeleted = Convert.ToInt32(reader["IsDeleted"]) == 1;
            
            return entity;
        }

        /// <summary>
        /// Serializes the generic value to bytes.
        /// </summary>
        protected virtual byte[] SerializeValue(T value)
        {
            if (value == null)
                return null;
                
            if (this.valueSerializer != null)
            {
                var serialized = this.valueSerializer.Serialize(value);
                return Encoding.UTF8.GetBytes(serialized);
            }
            
            // Fallback to JSON serialization
            var json = JsonSerializer.Serialize(value);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes the value from bytes.
        /// </summary>
        protected virtual T DeserializeValue(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;
                
            if (this.valueSerializer != null)
            {
                var serialized = Encoding.UTF8.GetString(bytes);
                return this.valueSerializer.Deserialize(serialized);
            }
            
            // Fallback to JSON deserialization
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Serializes tags to a string representation.
        /// </summary>
        protected virtual string SerializeTags(HashSet<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return null;
                
            return string.Join(";", tags);
        }

        /// <summary>
        /// Deserializes tags from string representation.
        /// </summary>
        protected virtual HashSet<string> DeserializeTags(string tags)
        {
            if (string.IsNullOrEmpty(tags))
                return new HashSet<string>();
                
            return new HashSet<string>(tags.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Serializes metadata dictionary to JSON.
        /// </summary>
        protected virtual string SerializeMetadata(Dictionary<string, string> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return null;
                
            return JsonSerializer.Serialize(metadata);
        }

        /// <summary>
        /// Deserializes metadata from JSON.
        /// </summary>
        protected virtual Dictionary<string, string> DeserializeMetadata(string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
                return new Dictionary<string, string>();
                
            return JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
        }
    }

    /// <summary>
    /// Non-generic CacheEntryMapper for when the value type is not known at compile time.
    /// </summary>
    public class CacheEntryMapper : CacheEntryMapper<object>
    {
        public CacheEntryMapper(string tableName = "CacheEntry") : base(null, tableName)
        {
        }

        /// <summary>
        /// Creates a typed CacheEntryMapper for a specific type at runtime.
        /// </summary>
        public static object CreateTypedMapper(Type valueType, string tableName = "CacheEntry")
        {
            var mapperType = typeof(CacheEntryMapper<>).MakeGenericType(valueType);
            return Activator.CreateInstance(mapperType, null, tableName);
        }
    }
}
```

### Usage Examples

```csharp
// Example 1: Strongly typed cache for UpdateEntity
// CacheEntryMapper will automatically use SerializerResolver to get the appropriate serializer
var updateEntityMapper = new CacheEntryMapper<UpdateEntity>();
var updateEntityProvider = new SqlitePersistenceProvider<CacheEntry<UpdateEntity>>(
    connectionString,
    updateEntityMapper
);

// Example 1a: With custom serializer
var customSerializer = new ProtobufSerializer<UpdateEntity>();
var updateEntityMapperCustom = new CacheEntryMapper<UpdateEntity>(customSerializer);

// Create table
await using (var conn = new SQLiteConnection(connectionString))
{
    await conn.OpenAsync();
    var createTableSql = updateEntityMapper.GenerateCreateTableSql();
    using var cmd = new SQLiteCommand(createTableSql, conn);
    await cmd.ExecuteNonQueryAsync();
    
    // Create indexes
    foreach (var indexSql in updateEntityMapper.GenerateCreateIndexSql())
    {
        using var indexCmd = new SQLiteCommand(indexSql, conn);
        await indexCmd.ExecuteNonQueryAsync();
    }
}

// Cache an entity
var cacheEntry = new CacheEntry<UpdateEntity>
{
    Id = "update-123",
    Value = new UpdateEntity { Key = "update-123", Type = "OEM" },
    TypeName = "UpdateEntity",
    TTLSeconds = 3600,
    Tags = new HashSet<string> { "oem", "critical" },
    Priority = 1
};

await updateEntityProvider.CreateAsync(cacheEntry);

// Example 2: Generic object cache
var objectMapper = new CacheEntryMapper();
var objectProvider = new SqlitePersistenceProvider<CacheEntry<object>>(
    connectionString,
    objectMapper
);

// Cache any object
var genericEntry = new CacheEntry<object>
{
    Id = "config-app",
    Value = new { Setting1 = "value1", Setting2 = 42 },
    TypeName = "AppConfig",
    TTLSeconds = 86400
};

await objectProvider.CreateAsync(genericEntry);

// Example 3: Runtime type creation
Type runtimeType = typeof(MyDynamicType);
var typedMapper = CacheEntryMapper.CreateTypedMapper(runtimeType);
```

## E.4 Serialization Implementation

### ISerializer Interface

```csharp
using System;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization
{
    /// <summary>
    /// Defines the contract for entity serialization.
    /// </summary>
    /// <typeparam name="T">The type of entity to serialize</typeparam>
    public interface ISerializer<T>
    {
        /// <summary>
        /// Serializes an entity to a string representation.
        /// </summary>
        /// <param name="entity">The entity to serialize</param>
        /// <returns>The serialized string representation</returns>
        string Serialize(T entity);

        /// <summary>
        /// Deserializes a string representation back to an entity.
        /// </summary>
        /// <param name="serialized">The serialized string</param>
        /// <returns>The deserialized entity</returns>
        T Deserialize(string serialized);

        /// <summary>
        /// Gets the type identifier for this serializer.
        /// </summary>
        string SerializerType { get; }
    }
}
```

### JsonSerializer Implementation

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization
{
    /// <summary>
    /// JSON-based serializer implementation using System.Text.Json.
    /// </summary>
    /// <typeparam name="T">The type of entity to serialize</typeparam>
    public class JsonSerializer<T> : ISerializer<T>
    {
        private readonly JsonSerializerOptions options;
        private readonly string serializerType;

        public JsonSerializer(JsonConverter customConverter = null)
        {
            this.options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = 
                {
                    new JsonStringEnumConverter()
                }
            };

            if (customConverter != null)
            {
                this.options.Converters.Add(customConverter);
                this.serializerType = $"JSON:{customConverter.GetType().Name}";
            }
            else
            {
                this.serializerType = "JSON";
            }
        }

        /// <inheritdoc/>
        public string Serialize(T entity)
        {
            if (entity == null)
                return null;

            return JsonSerializer.Serialize(entity, this.options);
        }

        /// <inheritdoc/>
        public T Deserialize(string serialized)
        {
            if (string.IsNullOrEmpty(serialized))
                return default(T);

            return JsonSerializer.Deserialize<T>(serialized, this.options);
        }

        /// <inheritdoc/>
        public string SerializerType => this.serializerType;
    }
}
```

### DataContractSerializer Implementation

```csharp
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization
{
    /// <summary>
    /// DataContract-based serializer implementation.
    /// </summary>
    /// <typeparam name="T">The type of entity to serialize</typeparam>
    public class DataContractSerializer<T> : ISerializer<T>
    {
        private readonly DataContractSerializer serializer;
        private readonly XmlWriterSettings writerSettings;
        private readonly XmlReaderSettings readerSettings;

        public DataContractSerializer()
        {
            this.serializer = new DataContractSerializer(typeof(T));
            
            this.writerSettings = new XmlWriterSettings
            {
                Indent = false,
                OmitXmlDeclaration = true,
                Encoding = Encoding.UTF8
            };

            this.readerSettings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
        }

        /// <inheritdoc/>
        public string Serialize(T entity)
        {
            if (entity == null)
                return null;

            using (var stringWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, this.writerSettings))
                {
                    this.serializer.WriteObject(xmlWriter, entity);
                    xmlWriter.Flush();
                    return stringWriter.ToString();
                }
            }
        }

        /// <inheritdoc/>
        public T Deserialize(string serialized)
        {
            if (string.IsNullOrEmpty(serialized))
                return default(T);

            using (var stringReader = new StringReader(serialized))
            {
                using (var xmlReader = XmlReader.Create(stringReader, this.readerSettings))
                {
                    return (T)this.serializer.ReadObject(xmlReader);
                }
            }
        }

        /// <inheritdoc/>
        public string SerializerType => "DataContract";
    }
}
```

### SerializerResolver

```csharp
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization
{
    /// <summary>
    /// Resolves the appropriate serializer for a given type based on attributes.
    /// </summary>
    public static class SerializerResolver
    {
        /// <summary>
        /// Gets an appropriate serializer for the specified type.
        /// </summary>
        /// <typeparam name="T">The type to get a serializer for</typeparam>
        /// <returns>An appropriate serializer instance</returns>
        public static ISerializer<T> GetSerializer<T>()
        {
            var type = typeof(T);

            // Check for JsonConverter attribute
            var jsonConverterAttr = type.GetCustomAttribute<JsonConverterAttribute>();
            if (jsonConverterAttr != null)
            {
                var converterType = jsonConverterAttr.ConverterType;
                var converter = Activator.CreateInstance(converterType) as JsonConverter;
                return new JsonSerializer<T>(converter);
            }

            // Check for DataContract attribute
            if (type.GetCustomAttribute<DataContractAttribute>() != null)
            {
                return new DataContractSerializer<T>();
            }

            // Default to JSON serialization
            return new JsonSerializer<T>();
        }

        /// <summary>
        /// Creates a serializer instance for the specified serializer type.
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="serializerType">The serializer type string</param>
        /// <returns>A serializer instance</returns>
        public static ISerializer<T> CreateSerializer<T>(string serializerType)
        {
            if (string.IsNullOrEmpty(serializerType))
                return GetSerializer<T>();

            if (serializerType == "DataContract")
                return new DataContractSerializer<T>();

            if (serializerType.StartsWith("JSON:"))
            {
                // Handle custom JSON converters
                var converterTypeName = serializerType.Substring(5);
                // In production, implement converter type resolution
                return new JsonSerializer<T>();
            }

            return new JsonSerializer<T>();
        }
    }
}
```

### Usage with Entities

```csharp
// Example 1: Entity with DataContract serialization
[DataContract(Name = "CacheEntry", Namespace = "http://schemas.microsoft.com/azurestack/update")]
public class CacheEntry<T> : BaseEntity<string> where T : class
{
    [DataMember(Order = 1)]
    [JsonPropertyName("typeName")]
    public string TypeName { get; set; }

    [DataMember(Order = 2)]
    [JsonPropertyName("value")]
    public T Value { get; set; }

    [DataMember(Order = 3)]
    [JsonPropertyName("ttlSeconds")]
    public int? TTLSeconds { get; set; }

    [DataMember(Order = 4)]
    [JsonPropertyName("tags")]
    public HashSet<string> Tags { get; set; } = new HashSet<string>();
}

// Example 2: Entity with custom JSON converter
[JsonConverter(typeof(UpdateEntityJsonConverter))]
public class UpdateEntity : BaseEntity<string>
{
    public string UpdateName { get; set; }
    public UpdateState State { get; set; }
    public Dictionary<string, string> Properties { get; set; }
}

// Example 3: Using SerializerResolver
public class PersistenceExample
{
    public void SaveEntity<T>(T entity) where T : class
    {
        // SerializerResolver automatically picks the right serializer
        var serializer = SerializerResolver.GetSerializer<T>();
        
        // For CacheEntry<T>, it will use DataContractSerializer
        // For UpdateEntity, it will use JsonSerializer with custom converter
        // For other types, it will use default JsonSerializer
        
        string serialized = serializer.Serialize(entity);
        string serializerType = serializer.SerializerType;
        
        // Store serialized data and serializer type
        SaveToDatabase(serialized, serializerType);
    }
    
    public T LoadEntity<T>(string data, string serializerType) where T : class
    {
        // Create the appropriate serializer based on stored type
        var serializer = SerializerResolver.CreateSerializer<T>(serializerType);
        return serializer.Deserialize(data);
    }
}
```

## E.5 Entity Mapper Interface and Implementation

### ISQLiteEntityMapper Interface

```csharp
namespace Microsoft.AzureStack.Services.Update.Common.Persistence.SQLite
{
    /// <summary>
    /// Defines the contract for mapping entities to SQLite tables.
    /// </summary>
    public interface ISQLiteEntityMapper<T, TKey>
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        string GetTableName();
        string GetPrimaryKeyColumn();
        List<string> GetSelectColumns();
        List<string> GetInsertColumns();
        List<string> GetUpdateColumns();
        void AddParameters(SQLiteCommand command, T entity);
        T MapFromReader(IDataReader reader);
        string SerializeKey(TKey key);
        TKey DeserializeKey(string serialized);
    }
}
```

## E.4 Expression Translation to SQL

### SQLiteExpressionTranslator Implementation

```csharp
namespace Microsoft.AzureStack.Services.Update.Common.Persistence.SQLite
{
    /// <summary>
    /// Translates LINQ Expression trees to SQL WHERE clauses.
    /// </summary>
    public class SQLiteExpressionTranslator<T> : ExpressionVisitor
    {
        private readonly StringBuilder sql = new StringBuilder();
        private readonly Dictionary<string, object> parameters = new Dictionary<string, object>();
        private int parameterIndex = 0;

        public class TranslationResult
        {
            public string Sql { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
        }

        public TranslationResult Translate(Expression<Func<T, bool>> expression)
        {
            this.Visit(expression.Body);
            return new TranslationResult
            {
                Sql = this.sql.ToString(),
                Parameters = this.parameters
            };
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            this.sql.Append("(");
            
            this.Visit(node.Left);
            
            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    this.sql.Append(" = ");
                    break;
                case ExpressionType.NotEqual:
                    this.sql.Append(" <> ");
                    break;
                case ExpressionType.LessThan:
                    this.sql.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    this.sql.Append(" <= ");
                    break;
                case ExpressionType.GreaterThan:
                    this.sql.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    this.sql.Append(" >= ");
                    break;
                case ExpressionType.AndAlso:
                    this.sql.Append(" AND ");
                    break;
                case ExpressionType.OrElse:
                    this.sql.Append(" OR ");
                    break;
                default:
                    throw new NotSupportedException($"Binary operator {node.NodeType} is not supported");
            }
            
            this.Visit(node.Right);
            
            this.sql.Append(")");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                // This is a property access on the parameter (e.g., x.UpdateName)
                this.sql.Append(this.GetColumnName(node.Member.Name));
            }
            else
            {
                // This is a constant value access
                var value = this.GetValue(node);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = value;
                this.sql.Append(paramName);
            }
            
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var paramName = $"@p{this.parameterIndex++}";
            this.parameters[paramName] = node.Value;
            this.sql.Append(paramName);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains")
            {
                if (node.Object != null)
                {
                    // String.Contains
                    this.Visit(node.Object);
                    this.sql.Append(" LIKE ");
                    var value = GetValue(node.Arguments[0]);
                    var paramName = $"@p{this.parameterIndex++}";
                    this.parameters[paramName] = $"%{value}%";
                    this.sql.Append(paramName);
                }
                else if (node.Arguments.Count == 2)
                {
                    // List.Contains
                    this.Visit(node.Arguments[1]);
                    this.sql.Append(" IN (");
                    var values = GetValue(node.Arguments[0]) as IEnumerable;
                    var paramNames = new List<string>();
                    foreach (var value in values)
                    {
                        var paramName = $"@p{this.parameterIndex++}";
                        this.parameters[paramName] = value;
                        paramNames.Add(paramName);
                    }
                    this.sql.Append(string.Join(", ", paramNames));
                    this.sql.Append(")");
                }
            }
            else if (node.Method.Name == "StartsWith")
            {
                Visit(node.Object);
                this.sql.Append(" LIKE ");
                var value = GetValue(node.Arguments[0]);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = $"{value}%";
                this.sql.Append(paramName);
            }
            else if (node.Method.Name == "EndsWith")
            {
                Visit(node.Object);
                this.sql.Append(" LIKE ");
                var value = GetValue(node.Arguments[0]);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = $"%{value}";
                this.sql.Append(paramName);
            }
            else
            {
                throw new NotSupportedException($"Method {node.Method.Name} is not supported");
            }
            
            return node;
        }

        private string GetColumnName(string propertyName)
        {
            // Map property names to column names
            // In a real implementation, this would use the entity mapper
            return propertyName switch
            {
                "Id" => this.GetPrimaryKeyColumn(),
                "Key" => this.GetPrimaryKeyColumn(),
                _ => propertyName
            };
        }

        private string GetPrimaryKeyColumn()
        {
            // This would be determined by the mapper
            var type = typeof(T);
            if (type.Name == "UpdateEntity") return "UpdateId";
            if (type.Name == "CacheEntry") return "CacheKey";
            return "Id";
        }

        private object GetValue(Expression expression)
        {
            var lambda = Expression.Lambda(expression);
            var compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }
    }
}
```

## E.5 BaseEntityMapper Usage and Tests

### Usage Example

```csharp
// Create mapper for UpdateEntity
var updateMapper = new BaseEntityMapper<UpdateEntity>();

// Generate CREATE TABLE statement
string createTableSql = updateMapper.GenerateCreateTableSql();
Console.WriteLine("=== CREATE TABLE SQL ===");
Console.WriteLine(createTableSql);

// Generate CREATE INDEX statements
var createIndexSqls = updateMapper.GenerateCreateIndexSql();
Console.WriteLine("=== CREATE INDEX SQL ===");
foreach (var indexSql in createIndexSqls)
{
    Console.WriteLine(indexSql);
}

// Get property mappings
var mappings = updateMapper.GetPropertyMappings();
Console.WriteLine("=== PROPERTY MAPPINGS ===");
foreach (var mapping in mappings)
{
    var propMapping = mapping.Value;
    Console.WriteLine($"Property: {propMapping.PropertyName}");
    Console.WriteLine($"  Column: {propMapping.ColumnName}");
    Console.WriteLine($"  Type: {propMapping.SqlType}");
    Console.WriteLine($"  Nullable: {propMapping.IsNullable}");
    Console.WriteLine($"  Primary Key: {propMapping.IsPrimaryKey}");
    if (propMapping.DefaultValue != null)
    {
        Console.WriteLine($"  Default: {propMapping.DefaultValue}");
    }
}
```

### Test Examples

```csharp
[TestClass]
public class BaseEntityMapperTests
{
    [TestMethod]
    public void Constructor_SimpleEntity_CreatesValidMappings()
    {
        // Arrange & Act
        var mapper = new BaseEntityMapper<SimpleEntity>();

        // Assert
        var mappings = mapper.GetPropertyMappings();
        Assert.AreEqual(3, mappings.Count); // Id, Name, CreatedDate
        
        var idMapping = mappings.Values.First(m => m.PropertyName == "Id");
        Assert.IsTrue(idMapping.IsPrimaryKey);
        Assert.AreEqual("Id", idMapping.ColumnName);
        Assert.AreEqual(SqlDbType.Int, idMapping.SqlType);
        
        var nameMapping = mappings.Values.First(m => m.PropertyName == "Name");
        Assert.AreEqual("Name", nameMapping.ColumnName);
        Assert.AreEqual(SqlDbType.NVarChar, nameMapping.SqlType);
        Assert.AreEqual(255, nameMapping.Size);
    }

    [TestMethod]
    public void GenerateCreateTableSql_EntityWithConstraints_IncludesConstraints()
    {
        // Arrange
        var mapper = new BaseEntityMapper<ConstrainedEntity>();

        // Act
        var sql = mapper.GenerateCreateTableSql();

        // Assert
        Assert.IsTrue(sql.Contains("Email TEXT NOT NULL UNIQUE"));
        Assert.IsTrue(sql.Contains("Age INTEGER CHECK (Age >= 0 AND Age <= 150)"));
        Assert.IsTrue(sql.Contains("Status TEXT DEFAULT 'Active'"));
    }

    [TestMethod]
    public void Constructor_CompositeKeyEntity_HandlesCompositeKey()
    {
        // Arrange & Act
        var mapper = new BaseEntityMapper<CompositeKeyEntity>();

        // Assert
        var compositeKeys = mapper.GetCompositeKeyMappings().ToList();
        Assert.AreEqual(2, compositeKeys.Count);
        Assert.IsTrue(compositeKeys.Any(m => m.ColumnName == "TenantId"));
        Assert.IsTrue(compositeKeys.Any(m => m.ColumnName == "UserId"));
    }

    // Test entities
    private class SimpleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private class ConstrainedEntity
    {
        public int Id { get; set; }

        [Column(IsNullable = false)]
        [Unique]
        public string Email { get; set; }

        [Check("Age >= 0 AND Age <= 150")]
        public int Age { get; set; }

        [Column(DefaultValue = "Active")]
        public string Status { get; set; }
    }

    private class CompositeKeyEntity
    {
        [PrimaryKey(IsComposite = true, Order = 1)]
        public int TenantId { get; set; }

        [PrimaryKey(IsComposite = true, Order = 2)]
        public int UserId { get; set; }

        public string Data { get; set; }
    }
}
```

## E.6 Key Translation Patterns

1. **CRUD Operation Signatures to SQL Mapping**:
   - `Get = Func<TKey, T>` → `SELECT ... WHERE PrimaryKey = @key ORDER BY Version DESC LIMIT 1` (then check IsDeleted and return null if true)
   - `Create = Func<T, T>` → `INSERT INTO ... VALUES (...)` with version from global Version table
   - `Update = Func<T, T>` → `UPDATE ... SET ... WHERE PrimaryKey = @key AND Version = @version` with new version from global Version table
   - `Delete = Func<TKey, bool>` → `UPDATE ... SET IsDeleted = 1` (soft delete, no version change) or `DELETE FROM ...` (hard delete)

2. **Parameter Safety**:
   - All values are passed as SQLite parameters (e.g., `@key`, `@UpdateName`)
   - Prevents SQL injection attacks
   - Handles proper type conversion and NULL values

3. **Optimistic Concurrency**:
   - Version field is checked on updates
   - New version obtained from global Version table for create/update operations
   - ConcurrencyException thrown on version mismatch
   - Soft deletes do not change version number

4. **Expression Translation**:
   - LINQ expressions are parsed and converted to SQL WHERE clauses
   - Support for common operators (==, !=, <, >, <=, >=, &&, ||)
   - String methods (Contains, StartsWith, EndsWith) mapped to LIKE patterns
   - Collection Contains mapped to SQL IN clause

5. **Type Mapping**:
   - DateTimeOffset stored as Unix timestamps (INTEGER)
   - Enums stored as strings or integers based on configuration
   - Complex types (lists, dictionaries) serialized as JSON
   - Nullable types handled with DBNull.Value