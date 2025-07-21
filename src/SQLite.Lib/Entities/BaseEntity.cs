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

    public abstract class BaseEntity<TKey> : IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
       /// Gets or sets the cache key (primary key).
       /// </summary>
        [DataMember]
        [JsonProperty("CacheKey")]
        public TKey Id { get; set; }

        [DataMember]
        [JsonProperty("CreatedTime")]
        public DateTimeOffset CreatedTime { get; set; }

        [DataMember]
        [JsonProperty("LastWriteTime")]
        public DateTimeOffset LastWriteTime { get; set; }

        [DataMember]
        [JsonProperty("Version")]
        public long Version { get; set; }

        [DataMember]
        [JsonProperty("IsDeleted")]
        public bool IsDeleted { get; set; }

        [DataMember]
        [JsonProperty("ExpirationTime")]
        public DateTimeOffset? ExpirationTime { get; set; }

        public long EstimateEntitySize()
        {
            return MemorySizeEstimator.EstimateObjectSize(this);
        }
    }
}