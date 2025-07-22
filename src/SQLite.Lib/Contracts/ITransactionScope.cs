// -----------------------------------------------------------------------
// <copyright file="ITransactionScope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a transaction scope that manages a collection of transactional operations.
    /// The scope is created by a persistence provider and handles SQL translation internally.
    /// </summary>
    public interface ITransactionScope<T, TKey> : IDisposable
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the unique transaction identifier.
        /// </summary>
        string TransactionId { get; }

        /// <summary>
        /// Gets the current state of the transaction.
        /// </summary>
        TransactionState State { get; }

        /// <summary>
        /// Gets the time when the transaction started.
        /// </summary>
        DateTimeOffset StartTime { get; }

        /// <summary>
        /// Adds a forward operation to the transaction.
        /// Operations are chained - output of one becomes input of the next.
        /// </summary>
        /// <param name="operation">The forward operation</param>
        void AddOperation(ITransactionalOperation<T, T> operation);

        /// <summary>
        /// Adds multiple operations that will be chained together.
        /// </summary>
        /// <param name="operations">List of operations to execute in order</param>
        void AddOperations(IEnumerable<ITransactionalOperation<T, T>> operations);

    }
}