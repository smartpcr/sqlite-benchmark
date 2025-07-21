// -----------------------------------------------------------------------
// <copyright file="TransactionBatch.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using System.Collections.Generic;
    using SQLite.Lib.Contracts;
    /// <summary>
    /// Manages a batch of transactional operations
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public class TransactionBatch<T> : ITransactionBatch<T> where T : class
    {
        private readonly IPersistenceProvider<,> provider;
        private readonly List<ITransactionalOperation<T>> operations;
        private readonly IDisposable transaction;
        private bool disposed;
        private bool committed;

        public IReadOnlyList<ITransactionalOperation<T>> Operations => this.operations.AsReadOnly();

        public TransactionBatch(IPersistenceProvider<,> provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.operations = new List<ITransactionalOperation<T>>();
            this.transaction = this.provider.BeginTransaction();
        }

        public void AddInsert(T entity)
        {
            this.ThrowIfDisposed();
            this.operations.Add(TransactionalOperation<T>.CreateInsert(entity));
        }

        public void AddUpdate(T entity)
        {
            this.ThrowIfDisposed();
            this.operations.Add(TransactionalOperation<T>.CreateUpdate(entity));
        }

        public void AddDelete(long entityId)
        {
            this.ThrowIfDisposed();
            this.operations.Add(TransactionalOperation<T>.CreateDelete(entityId));
        }

        public void Commit()
        {
            this.ThrowIfDisposed();
            
            if (this.committed)
                throw new InvalidOperationException("Transaction has already been committed");

            try
            {
                // Execute all operations in order
                foreach (var operation in this.operations)
                {
                    operation.Commit(this.provider);
                }

                this.committed = true;
            }
            catch
            {
                // If any operation fails, the transaction will be rolled back
                // when the transaction is disposed
                throw;
            }
        }

        public void Rollback()
        {
            this.ThrowIfDisposed();
            
            // Rollback each operation (though in SQLite this is mainly for cleanup)
            foreach (var operation in this.operations)
            {
                operation.Rollback();
            }
            
            // The actual SQLite rollback happens when the transaction is disposed
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            try
            {
                if (!this.committed)
                {
                    // If not committed, ensure rollback
                    this.Rollback();
                }
            }
            finally
            {
                this.transaction?.Dispose();
                this.disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(TransactionBatch<T>));
        }
    }
}