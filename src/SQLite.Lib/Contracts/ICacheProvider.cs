// -----------------------------------------------------------------------
// <copyright file="ICacheProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SQLite.Lib.Entities.Caches;

    /// <summary>
    /// Defines the contract for a cache provider that stores key-value pairs with expiration support.
    /// </summary>
    /// <typeparam name="T">The type of value to cache</typeparam>
    public interface ICacheProvider<T> where T : class, IEntity<string>
    {
        /// <summary>
        /// Gets a cached value by key.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The cached value or null if not found or expired</returns>
        Task<T> GetAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache with the specified key.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="expiration">Optional expiration time span</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SetAsync(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache with sliding expiration.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="slidingExpiration">The sliding expiration time span</param>
        /// <param name="absoluteExpiration">Optional absolute expiration time span</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SetWithSlidingExpirationAsync(string key, T value, TimeSpan slidingExpiration, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if the item was removed, false otherwise</returns>
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>True if the key exists and is not expired, false otherwise</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all cache entries with a specific tag.
        /// </summary>
        /// <param name="tag">The tag to search for</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>List of cache entries with the specified tag</returns>
        Task<IList<CacheEntry<T>>> GetByTagAsync(string tag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all expired entries from the cache.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The number of expired entries that were cleared</returns>
        Task<int> ClearExpiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes the cache database schema.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}