// -----------------------------------------------------------------------
// <copyright file="ITransactionalOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a transactional operation that can be committed or rolled back.
    /// Generic version supports entity-specific operations with type safety.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The key type</typeparam>
    public interface ITransactionalOperation<T, TKey>
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the unique identifier for this operation.
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// Gets the operation description for logging.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Commits the operation with original and new values.
        /// </summary>
        /// <param name="originalValue">The original entity value (null for creates)</param>
        /// <param name="newValue">The new entity value (null for deletes)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CommitAsync(T originalValue, T newValue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the operation with original and new values.
        /// </summary>
        /// <param name="originalValue">The original entity value</param>
        /// <param name="newValue">The new entity value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RollbackAsync(T originalValue, T newValue, CancellationToken cancellationToken = default);
    }
}