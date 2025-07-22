// -----------------------------------------------------------------------
// <copyright file="EntryListMapping.cs" company="Microsoft Corp.">
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
    /// Represents the mapping between a list cache key and individual entry cache keys.
    /// </summary>
    /// <remarks>
    /// The Version field in this table represents the version when the list was created.
    /// Individual entries referenced by EntryCacheKey may be updated independently after
    /// the list is created, so their versions in the main entity table may differ from
    /// the version stored in this mapping table.
    /// </remarks>
    [Table("EntryListMapping")]
    public class EntryListMapping : BaseEntity<string>
    {
        /// <summary>
        /// Gets or sets the composite key (not mapped to database).
        /// </summary>
        [NotMapped]
        public new string Id
        {
            get => $"{this.ListCacheKey}:{this.EntryCacheKey}";
            set { }
        }

        /// <summary>
        /// Gets or sets whether the entity is deleted (not mapped - this table doesn't support soft delete).
        /// </summary>
        [NotMapped]
        public new bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the list cache key (composite primary key part 1).
        /// </summary>
        [DataMember]
        [JsonProperty("ListCacheKey")]
        [PrimaryKey(Order = 1)]
        [Column("ListCacheKey", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_EntryListMapping_ListCacheKey")]
        public string ListCacheKey { get; set; }

        /// <summary>
        /// Gets or sets the entry cache key (composite primary key part 2).
        /// </summary>
        [DataMember]
        [JsonProperty("EntryCacheKey")]
        [PrimaryKey(Order = 2)]
        [Column("EntryCacheKey", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_EntryListMapping_EntryCacheKey")]
        public string EntryCacheKey { get; set; }

        /// <summary>
        /// Gets or sets the caller file path for auditing.
        /// </summary>
        [DataMember]
        [JsonProperty("CallerFile")]
        [Column("CallerFile", SQLiteDbType.Text)]
        public string CallerFile { get; set; }

        /// <summary>
        /// Gets or sets the caller member name for auditing.
        /// </summary>
        [DataMember]
        [JsonProperty("CallerMember")]
        [Column("CallerMember", SQLiteDbType.Text)]
        public string CallerMember { get; set; }

        /// <summary>
        /// Gets or sets the caller line number for auditing.
        /// </summary>
        [DataMember]
        [JsonProperty("CallerLineNumber")]
        [Column("CallerLineNumber", SQLiteDbType.Integer)]
        public int? CallerLineNumber { get; set; }
    }
}