// -----------------------------------------------------------------------
// <copyright file="ITransactionScope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a transaction scope that manages a collection of transactional operations.
    /// The scope is created by a persistence provider and handles SQL translation internally.
    /// </summary>
    public interface ITransactionScope : IDisposable
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
        void AddOperation(ITransactionalOperation operation);

        /// <summary>
        /// Adds multiple operations that will be chained together.
        /// </summary>
        /// <param name="operations">List of operations to execute in order</param>
        void AddOperations(IEnumerable<ITransactionalOperation> operations);

        /// <summary>
        /// Executes all operations in the transaction.
        /// </summary>
        Task<bool> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}