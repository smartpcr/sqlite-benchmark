// -----------------------------------------------------------------------
// <copyright file="Version.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using SQLite.Lib.Entities;
    using SQLite.Lib.Mappings;

    /// <summary>
    /// Represents a version entry in the global version sequence table.
    /// </summary>
    [DataContract]
    [Table("Version")]
    public class VersionEntity : BaseEntity<long>
    {
        /// <summary>
        /// Override Id to map to Version column as primary key.
        /// </summary>
        [DataMember]
        [JsonProperty("Version")]
        [PrimaryKey(IsAutoIncrement = true)]
        [Column("Version", SQLiteDbType.Integer, NotNull = true)]
        public new long Id { get; set; }

        /// <summary>
        /// Override Version property from BaseEntity - not mapped in this table.
        /// </summary>
        [NotMapped]
        public new long Version { get; set; }

        /// <summary>
        /// Override CreatedTime property from BaseEntity - not mapped in this table.
        /// </summary>
        [NotMapped]
        public new DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Override LastWriteTime property from BaseEntity - not mapped in this table.
        /// </summary>
        [NotMapped]
        public new DateTimeOffset LastWriteTime { get; set; }

        /// <summary>
        /// Override IsDeleted property from BaseEntity - not mapped in this table.
        /// </summary>
        [NotMapped]
        public new bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this version was created.
        /// </summary>
        [DataMember]
        [JsonProperty("Timestamp")]
        [Column("Timestamp", SQLiteDbType.Text, NotNull = true)]
        [Computed(Expression = "datetime('now')")]
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Creates a new version entry.
        /// </summary>
        /// <returns>A new Version instance.</returns>
        public static VersionEntity Create()
        {
            return new VersionEntity
            {
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// Estimates the memory size of this entity.
        /// </summary>
        /// <returns>The estimated memory size in bytes.</returns>
        public new long EstimateEntitySize()
        {
            // Version entity is very small - just an ID and timestamp
            return 32; // Approximate size
        }
    }
}