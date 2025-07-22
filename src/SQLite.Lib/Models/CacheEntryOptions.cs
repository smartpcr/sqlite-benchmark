// -----------------------------------------------------------------------
// <copyright file="CacheEntryOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;

    /// <summary>
    /// Options for creating cache entries.
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Gets or sets an absolute expiration date for the cache entry.
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets how long a cache entry can be inactive before it will be removed.
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with this cache entry.
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Gets or sets the priority for cache eviction (future use).
        /// </summary>
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
    }
}