using System;
using System.Collections.Generic;

namespace SQLite.Lib.Contracts
{
    /// <summary>
    /// Represents a batch of transactional operations that can be executed as a single unit
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    public interface ITransactionBatch<T, TKey> : IDisposable where T : class, IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Adds an insert operation to the batch
        /// </summary>
        /// <param name="entity">Entity to insert</param>
        void AddInsert(T entity);

        /// <summary>
        /// Adds an update operation to the batch
        /// </summary>
        /// <param name="updateAction">Update applied to entity.</param>
        void AddUpdate(Action<T> updateAction);

        /// <summary>
        /// Adds a delete operation to the batch
        /// </summary>
        /// <param name="entityId">ID of entity to delete</param>
        void AddDelete(TKey entityId);

        /// <summary>
        /// Commits all operations in the batch
        /// </summary>
        void Commit();

        /// <summary>
        /// Rolls back all operations in the batch
        /// </summary>
        void Rollback();

        /// <summary>
        /// Gets the list of operations in the batch
        /// </summary>
        IReadOnlyList<ITransactionalOperation<T, TKey>> Operations { get; }
    }
}
