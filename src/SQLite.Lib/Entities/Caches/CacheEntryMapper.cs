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
    using System.Data.SQLite;
    using System.Text;
    using SQLite.Lib.Mappings;
    using SQLite.Lib.Serialization;

    /// <summary>
    /// SQLite mapper for CacheEntry<T> that handles generic type serialization.</T>
    /// </summary>
    public class CacheEntryMapper<T> : BaseEntityMapper<CacheEntry<T>> where T : class
    {
        private readonly ISerializer<T> valueSerializer;
        private readonly string tableName;

        public CacheEntryMapper(ISerializer<T> valueSerializer = null, string tableName = "CacheEntry")
        {
            this.valueSerializer = valueSerializer ?? SerializerResolver.CreateSerializer<T>();
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
}