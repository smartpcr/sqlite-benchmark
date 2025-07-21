// -----------------------------------------------------------------------
// <copyright file="IPersistenceProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the contract for persistence providers that handle entity storage and retrieval.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IPersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region CRUD Operations

        /// <summary>
        /// Creates a new entity in the persistence store.
        /// </summary>
        /// <param name="entity">The entity to create</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entity with updated tracking fields</returns>
        Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an entity by its primary key.
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The entity if found; otherwise null</returns>
        Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity with optimistic concurrency control.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entity with incremented version</returns>
        /// <exception cref="ConcurrencyException">Thrown when version conflict detected</exception>
        Task<T> UpdateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity by its primary key (soft delete by default).
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="hardDelete">If true, permanently removes the entity</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted; false if not found</returns>
        Task<bool> DeleteAsync(TKey key, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default);

        #endregion

        #region Batch Operations

        /// <summary>
        /// Creates multiple entities in a single batch operation.
        /// </summary>
        /// <param name="entities">The entities to create</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entities with updated tracking fields</returns>
        Task<IEnumerable<T>> CreateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves multiple entities by their primary keys.
        /// </summary>
        /// <param name="keys">The primary keys</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The found entities</returns>
        Task<IEnumerable<T>> GetBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities in a single batch operation.
        /// </summary>
        /// <param name="entities">The entities to update</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entities</returns>
        Task<IEnumerable<T>> UpdateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple entities by their primary keys.
        /// </summary>
        /// <param name="keys">The primary keys</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="hardDelete">If true, permanently removes the entities</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of entities deleted</returns>
        Task<int> DeleteBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default);

        #endregion

        #region Query Operations

        /// <summary>
        /// Queries entities based on a predicate expression.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entities matching the predicate</returns>
        Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries entities with pagination support.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="orderBy">Order by expression</param>
        /// <param name="ascending">Sort direction</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Page of entities</returns>
        Task<PagedResult<T>> QueryPagedAsync(
            Expression<Func<T, bool>> predicate,
            int pageSize,
            int pageNumber,
            Expression<Func<T, IComparable>> orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts entities matching a predicate.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Count of matching entities</returns>
        Task<long> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entity exists matching a predicate.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if any entity matches</returns>
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Imports entities in bulk from an external source.
        /// </summary>
        /// <param name="entities">The entities to import</param>
        /// <param name="options">Import options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Import result with statistics</returns>
        Task<BulkImportResult> BulkImportAsync(
            IEnumerable<T> entities,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports entities in bulk based on criteria.
        /// </summary>
        /// <param name="predicate">Filter for entities to export</param>
        /// <param name="options">Export options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with exported entities</returns>
        Task<BulkExportResult<T>> BulkExportAsync(
            Expression<Func<T, bool>> predicate = null,
            BulkExportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        #endregion

        #region Transaction Operations

        /// <summary>
        /// Begins a new transaction scope.
        /// </summary>
        /// <returns>Transaction scope for managing transactional operations</returns>
        Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Maintenance Operations

        /// <summary>
        /// Removes expired entities based on their ExpirationTime.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of entities removed</returns>
        Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Optimizes the underlying storage (e.g., vacuum, reindex).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task OptimizeStorageAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets storage statistics for monitoring.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Storage statistics</returns>
        Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}