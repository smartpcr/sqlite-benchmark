// -----------------------------------------------------------------------
// <copyright file="TransactionScope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SQLite.Lib.Contracts;

    /// <summary>
    /// Implementation of ITransactionScope that manages transactional operations.
    /// Created by persistence provider and handles SQL translation internally.
    /// </summary>
    public class TransactionScope : ITransactionScope
    {
        private readonly string connectionString;
        private readonly ConcurrentDictionary<(Type entityType, Type keyType), object> providers = new ConcurrentDictionary<(Type, Type), object>();
        private readonly List<ITransactionalOperation> operations = new List<ITransactionalOperation>();
        private readonly object lockObject = new object();
        private bool disposed;

        public string TransactionId { get; }
        public TransactionState State { get; private set; }
        public DateTimeOffset StartTime { get; }

        public TransactionScope(string connectionString)
        {
            this.connectionString = connectionString;
            this.TransactionId = Guid.NewGuid().ToString();
            this.State = TransactionState.Active;
            this.StartTime = DateTimeOffset.UtcNow;
        }

        public void AddOperation(ITransactionalOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {State} transaction.");

            lock (this.lockObject)
            {
                this.operations.Add(operation);
            }
        }

        public void AddOperations(IEnumerable<ITransactionalOperation> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {State} transaction.");

            lock (this.lockObject)
            {
                this.operations.AddRange(operations);
            }
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot execute a {State} transaction.");

            State = TransactionState.Committing;

            var reverseOperations = new Stack<(object result, object operation)>();
            object currentInput = null;

            try
            {
                // Execute forward operations in sequence, chaining outputs to inputs
                foreach (var transactionalOperation in this.operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    switch (transactionalOperation.ExecMode)
                    {
                        case SqlExecMode.ExecuteReader:

                    }
                }

                State = TransactionState.Committed;
                return true;
            }
            catch (Exception ex)
            {
                State = TransactionState.RollingBack;

                // Rollback in reverse order
                var rollbackErrors = new List<Exception>();

                while (reverseOperations.Count > 0)
                {
                    var (result, operation) = reverseOperations.Pop();

                    try
                    {
                        // Get the reverse operation
                        var operationType = operation.GetType();
                        var reverseProperty = operationType.GetProperty("ReverseOperation");

                        if (reverseProperty != null)
                        {
                            var reverseOperation = reverseProperty.GetValue(operation);
                            if (reverseOperation != null)
                            {
                                var reverseType = reverseOperation.GetType();
                                var reverseExecuteMethod = reverseType.GetMethod("ExecuteAsync");

                                if (reverseExecuteMethod != null)
                                {
                                    var reverseTask = reverseExecuteMethod.Invoke(
                                        reverseOperation,
                                        new object[] { result, this.provider, cancellationToken }) as Task;
                                    await reverseTask.ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        rollbackErrors.Add(new Exception(
                            $"Failed to rollback operation {operation.GetType().Name}: {rollbackEx.Message}",
                            rollbackEx));
                    }
                }

                State = TransactionState.Failed;

                if (rollbackErrors.Any())
                {
                    throw new AggregateException(
                        $"Transaction failed with error: {ex.Message}. Additionally, {rollbackErrors.Count} rollback operations failed.",
                        rollbackErrors.Prepend(ex));
                }

                throw;
            }
        }


        public void Dispose()
        {
            if (this.disposed)
                return;

            if (this.State == TransactionState.Active)
            {
                // Log warning - transaction was not executed
                // In production, this would log a warning that a transaction was disposed without execution
            }

            this.disposed = true;
        }

        private IPersistenceProvider<T, TKey> GetProvider<T, TKey>()
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            var key = (typeof(T), typeof(TKey));
            if (!this.providers.TryGetValue(key, out var provider))
            {
                throw new InvalidOperationException($"No provider registered for entity type {typeof(T).Name} with key type {typeof(TKey).Name}");
            }

            return (IPersistenceProvider<T, TKey>)provider;
        }
    }
}