// -----------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Entities
{
    using System;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Mappings;

    public abstract class BaseEntity<TKey> : IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
       /// Gets or sets the cache key (primary key).
       /// </summary>
        [DataMember]
        [JsonProperty("CacheKey")]
        [PrimaryKey(Order = 1)]
        [Column("CacheKey", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_CacheEntry_Key")]
        public TKey Id { get; set; }

        [DataMember]
        [JsonProperty("Version")]
        [PrimaryKey(Order = 2)]
        [AuditField(AuditFieldType.Version)]
        [Column("Version", SQLiteDbType.Integer, NotNull = true)]
        [Index("IX_CacheEntry_Version")]
        public long Version { get; set; }

        [DataMember]
        [JsonProperty("CreatedTime")]
        [AuditField(AuditFieldType.CreatedTime)]
        [Column("CreatedTime", SQLiteDbType.Text, NotNull = true)]
        public DateTimeOffset CreatedTime { get; set; }

        [DataMember]
        [JsonProperty("LastWriteTime")]
        [AuditField(AuditFieldType.LastWriteTime)]
        [Column("LastWriteTime", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_CacheEntry_LastWriteTime")]
        public DateTimeOffset LastWriteTime { get; set; }

        [DataMember]
        [JsonProperty("IsDeleted")]
        [AuditField(AuditFieldType.IsDeleted)]
        [Column("IsDeleted", SQLiteDbType.Integer, NotNull = true)]
        public bool IsDeleted { get; set; }

        public long EstimateEntitySize()
        {
            return MemorySizeEstimator.EstimateObjectSize(this);
        }
    }
}