// -----------------------------------------------------------------------
// <copyright file="IEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines the contract for persistable entities with strongly-typed keys.
    /// </summary>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        [JsonProperty("CacheKey")]
        TKey Id { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was created.
        /// </summary>
        DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was last modified.
        /// </summary>
        DateTimeOffset LastWriteTime { get; set; }

        /// <summary>
        /// Gets or sets the version number for optimistic concurrency control.
        /// </summary>
        long Version { get; set; }

        /// <summary>
        /// Gets or sets whether the entity has been soft deleted.
        /// </summary>
        bool IsDeleted { get; set; }

        long EstimateEntitySize();
    }
}
