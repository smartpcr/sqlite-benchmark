// -----------------------------------------------------------------------
// <copyright file="CacheEntry.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Entities;
    using SQLite.Lib.Mappings;

    /// <summary>
    /// Generic wrapper for cache entries that provides type-safe storage and retrieval
    /// with built-in metadata tracking and serialization support.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    [DataContract]
    [Table("CacheEntry")]
    public class CacheEntry<T> : BaseEntity<string> where T : class, IEntity<string>
    {
        /// <summary>
        /// Gets or sets the cached value.
        /// </summary>
        [DataMember]
        [JsonProperty("value")]
        [NotMapped] // The actual Value property is not mapped directly - we use serialization
        public T Value { get; set; }

        /// <summary>
        /// Gets or sets the serialized data for database storage.
        /// </summary>
        [DataMember]
        [JsonProperty("data")]
        [Column("Data", SQLiteDbType.Blob, NotNull = true)]
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets the type name of the cached value for deserialization.
        /// </summary>
        [DataMember]
        [JsonProperty("typeName")]
        [Column("TypeName", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_CacheEntry_Type")]
        [ForeignKey("CacheEntity", "TypeName", Name = "FK_CacheEntry_CacheEntity", Ordinal = 0)]
        public string TypeName { get; set; }


        /// <summary>
        /// Gets or sets the assembly version for type compatibility checking.
        /// </summary>
        [DataMember]
        [JsonProperty("assemblyVersion")]
        [Column("AssemblyVersion", SQLiteDbType.Text, NotNull = true)]
        [ForeignKey("CacheEntity", "AssemblyVersion", Name = "FK_CacheEntry_CacheEntity", Ordinal = 1)]
        public string AssemblyVersion { get; set; }

        /// <summary>
        /// Gets or sets the size of the serialized value in bytes.
        /// </summary>
        [DataMember]
        [JsonProperty("size")]
        [Column("Size", SQLiteDbType.Integer, NotNull = true)]
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the absolute expiration time for this cache entry.
        /// If set, the entry will expire at this specific time regardless of access patterns.
        /// </summary>
        [DataMember]
        [JsonProperty("absoluteExpiration")]
        [Column("AbsoluteExpiration", SQLiteDbType.Text)]
        [Index("IX_CacheEntry_AbsoluteExpiration")]
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets the sliding expiration window for this cache entry.
        /// If set, the entry will expire after this duration of inactivity.
        /// </summary>
        [DataMember]
        [JsonProperty("slidingExpiration")]
        [Column("SlidingExpirationSeconds", SQLiteDbType.Integer)]
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with this cache entry for metadata queries and bulk operations.
        /// </summary>
        [DataMember]
        [JsonProperty("tags")]
        [Column("Tags", SQLiteDbType.Text)]
        [Index("IX_CacheEntry_Tags")]
        public string[] Tags { get; set; }

        /// <summary>
        /// Creates a new CacheEntry&lt;T&gt; with the specified value and options.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="options">Cache entry options containing expiration and tagging settings.</param>
        /// <returns>A new CacheEntry&lt;T&gt; instance.</returns>
        public static CacheEntry<T> Create(string key, T value, CacheEntryOptions options = null)
        {
            var type = typeof(T);
            var entry = new CacheEntry<T>
            {
                Id = key,
                Value = value,
                TypeName = type.Name,
                AssemblyVersion = type.Assembly.GetName().Version?.ToString(),
                CreatedTime = DateTimeOffset.UtcNow,
                LastWriteTime = DateTimeOffset.UtcNow,
                Version = 1,
                IsDeleted = false
            };

            if (options != null)
            {
                entry.AbsoluteExpiration = options.AbsoluteExpiration;
                entry.SlidingExpiration = options.SlidingExpiration;
                entry.Tags = options.Tags;
            }

            return entry;
        }

        /// <summary>
        /// Updates the value and metadata of this cache entry.
        /// </summary>
        /// <param name="newValue">The new value</param>
        /// <param name="incrementVersion">Whether to increment the version number</param>
        public void UpdateValue(T newValue, bool incrementVersion = true)
        {
            this.Value = newValue;
            this.LastWriteTime = DateTimeOffset.UtcNow;

            if (incrementVersion)
            {
                this.Version++;
            }
        }

        /// <summary>
        /// Checks if this cache entry has expired based on its expiration settings.
        /// </summary>
        /// <returns>True if expired; otherwise false</returns>
        public bool IsExpired()
        {
            var now = DateTimeOffset.UtcNow;

            // Check absolute expiration
            if (this.AbsoluteExpiration.HasValue && now > this.AbsoluteExpiration.Value)
            {
                return true;
            }

            // Check sliding expiration
            if (this.SlidingExpiration.HasValue)
            {
                var slidingExpirationTime = this.LastWriteTime.Add(this.SlidingExpiration.Value);
                if (now > slidingExpirationTime)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Refreshes the sliding expiration window by updating the last write time.
        /// </summary>
        public void RefreshExpiration()
        {
            if (this.SlidingExpiration.HasValue)
            {
                this.LastWriteTime = DateTimeOffset.UtcNow;
            }
        }

    }
}