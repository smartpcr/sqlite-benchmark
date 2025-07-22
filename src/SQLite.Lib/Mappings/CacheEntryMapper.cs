// -----------------------------------------------------------------------
// <copyright file="CacheEntryMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Entities.Caches
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Mappings;
    using SQLite.Lib.Serialization;

    /// <summary>
    /// Specialized mapper for CacheEntry&lt;T&gt; entities that handles generic value serialization.
    /// </summary>
    /// <typeparam name="T">The type of value stored in the cache entry</typeparam>
    public class CacheEntryMapper<T> : BaseEntityMapper<CacheEntry<T>, string> where T : class, IEntity<string>
    {
        private readonly ISerializer<T> valueSerializer;
        private readonly string tableName;

        public CacheEntryMapper(ISerializer<T> valueSerializer = null, string tableName = "CacheEntry")
        {
            this.valueSerializer = valueSerializer ?? SerializerResolver.GetSerializer<T>();
            this.tableName = tableName;
        }

        /// <summary>
        /// Override to use custom table name (since CacheEntry&lt;T&gt; is generic).
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
            sql.AppendLine("    CacheKey TEXT NOT NULL,");
            sql.AppendLine("    Version INTEGER NOT NULL,");
            sql.AppendLine("    TypeName TEXT NOT NULL,");
            sql.AppendLine("    AssemblyVersion TEXT NOT NULL,");
            sql.AppendLine("    Data BLOB NOT NULL,");
            sql.AppendLine("    AbsoluteExpiration INTEGER,");
            sql.AppendLine("    Size INTEGER NOT NULL,");
            sql.AppendLine("    IsDeleted INTEGER NOT NULL DEFAULT 0,");
            sql.AppendLine("    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),");
            sql.AppendLine("    LastWriteTime TEXT NOT NULL DEFAULT (datetime('now')),");
            sql.AppendLine("    PRIMARY KEY (CacheKey, Version)");
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
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_AbsoluteExpiration ON {this.tableName} (AbsoluteExpiration);",
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_Version ON {this.tableName} (Version DESC);",
                $"CREATE INDEX IF NOT EXISTS IX_{this.tableName}_IsDeleted ON {this.tableName} (IsDeleted);"
            };

            return indexes;
        }

        /// <summary>
        /// Override to handle CacheEntry&lt;T&gt; specific column mappings.
        /// </summary>
        public override List<string> GetSelectColumns()
        {
            return new List<string>
            {
                "CacheKey",
                "Version",
                "TypeName",
                "AssemblyVersion",
                "Data",
                "AbsoluteExpiration",
                "Size",
                "IsDeleted",
                "CreatedTime",
                "LastWriteTime"
            };
        }

        /// <summary>
        /// Override to handle CacheEntry&lt;T&gt; specific insert columns.
        /// </summary>
        public override List<string> GetInsertColumns()
        {
            return new List<string>
            {
                "CacheKey",
                "Version",
                "TypeName",
                "AssemblyVersion",
                "Data",
                "AbsoluteExpiration",
                "Size",
                "IsDeleted",
                "CreatedTime",
                "LastWriteTime"
            };
        }

        /// <summary>
        /// Override to handle CacheEntry&lt;T&gt; specific update columns.
        /// </summary>
        public override List<string> GetUpdateColumns()
        {
            return new List<string>
            {
                "TypeName",
                "AssemblyVersion",
                "Data",
                "AbsoluteExpiration",
                "Size",
                "LastWriteTime"
                // Note: CacheKey and Version are primary keys (not updated)
                // Note: CreatedTime is not updated
                // Note: IsDeleted is handled separately
            };
        }

        /// <summary>
        /// Override to add parameters with proper value serialization.
        /// </summary>
        public override void AddParameters(System.Data.Common.DbCommand command, CacheEntry<T> entity)
        {
            // Serialize the entire CacheEntry<T> object
            byte[] serializedData = this.SerializeCacheEntry(entity);

            var param = command.CreateParameter();
            param.ParameterName = "@CacheKey";
            param.Value = entity.Id ?? (object)DBNull.Value;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@Version";
            param.Value = entity.Version;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@TypeName";
            param.Value = entity.TypeName ?? typeof(T).FullName;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@AssemblyVersion";
            param.Value = this.GetAssemblyVersion();
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@Data";
            param.Value = serializedData ?? (object)DBNull.Value;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@AbsoluteExpiration";
            param.Value = entity.ExpirationTime?.ToUnixTimeSeconds() ?? (object)DBNull.Value;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@Size";
            param.Value = serializedData?.Length ?? 0;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@IsDeleted";
            param.Value = entity.IsDeleted ? 1 : 0;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@CreatedTime";
            param.Value = entity.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss");
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@LastWriteTime";
            param.Value = entity.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            command.Parameters.Add(param);
        }

        /// <summary>
        /// Override to map from reader with proper value deserialization.
        /// </summary>
        public override CacheEntry<T> MapFromReader(IDataReader reader)
        {
            // Fallback: create from individual columns if deserialization fails
            var entity = new CacheEntry<T>();
            entity.Id = reader["CacheKey"] as string;
            entity.TypeName = reader["TypeName"] as string;
            entity.Version = reader["Version"] == DBNull.Value ? 0 : Convert.ToInt64(reader["Version"]);
            entity.Size = reader["Size"] == DBNull.Value ? 0 : Convert.ToInt64(reader["Size"]);
            entity.ExpirationTime = reader["AbsoluteExpiration"] == DBNull.Value ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reader["AbsoluteExpiration"]));
            entity.IsDeleted = reader["IsDeleted"] != DBNull.Value && Convert.ToInt32(reader["IsDeleted"]) == 1;
            entity.CreatedTime = DateTime.Parse(reader["CreatedTime"].ToString());
            entity.LastWriteTime = DateTime.Parse(reader["LastWriteTime"].ToString());

            // Deserialize the entire CacheEntry<T> from the Data column
            if (reader["Data"] is byte[] dataBytes)
            {
                entity.Value = this.valueSerializer.Deserialize(dataBytes);
            }

            return entity;
        }

        /// <summary>
        /// Serializes the entire CacheEntry&lt;T&gt; to bytes.
        /// </summary>
        public virtual byte[] SerializeCacheEntry(CacheEntry<T> entry)
        {
            if (entry == null)
                return null;

            return this.valueSerializer.Serialize(entry.Value);
        }

        /// <summary>
        /// Gets the assembly version for type tracking.
        /// </summary>
        protected virtual string GetAssemblyVersion()
        {
            var assembly = typeof(T).Assembly;
            return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        }

    }
}