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
    using System.Data.SQLite;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Traces;

    /// <summary>
    /// Implementation of ITransactionScope that manages transactional operations.
    /// Created by persistence provider and handles SQL translation internally.
    /// </summary>
    public class TransactionScope<T, TKey> : ITransactionScope<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly string connectionString;
        private readonly IPersistenceProvider<T, TKey> provider;
        private readonly List<ITransactionalOperation<T, T>> operations = new List<ITransactionalOperation<T, T>>();
        private readonly object lockObject = new object();
        private bool disposed;
        private bool shouldCommit = true; // Default to commit unless explicitly rolled back

        public string TransactionId { get; }
        public TransactionState State { get; private set; }
        public DateTimeOffset StartTime { get; }

        public TransactionScope(string connectionString, IPersistenceProvider<T, TKey> provider)
        {
            this.connectionString = connectionString;
            this.provider = provider;
            this.TransactionId = Guid.NewGuid().ToString();
            this.State = TransactionState.Active;
            this.StartTime = DateTimeOffset.UtcNow;
            
            Logger.TransactionStart();
        }

        public void AddOperation(ITransactionalOperation<T, T> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {this.State} transaction.");

            lock (this.lockObject)
            {
                this.operations.Add(operation);
            }
        }

        public void AddOperations(IEnumerable<ITransactionalOperation<T, T>> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {this.State} transaction.");

            lock (this.lockObject)
            {
                this.operations.AddRange(operations);
            }
        }

        /// <summary>
        /// Marks the transaction for rollback. The actual rollback will occur during disposal.
        /// </summary>
        public void Rollback()
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot rollback a {this.State} transaction.");

            this.shouldCommit = false;
            Logger.TransactionRollback();
        }

        /// <summary>
        /// Marks the transaction for commit. This is the default behavior.
        /// </summary>
        public void Commit()
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot commit a {this.State} transaction.");

            this.shouldCommit = true;
            Logger.TransactionCommit();
        }

        private async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot execute a {this.State} transaction.");

            this.State = TransactionState.Committing;
            var reverseOperations = new Stack<(T result, ITransactionalOperation<T, T> operation)>();

            try
            {
                using var connection = new SQLiteConnection(this.connectionString);
                // Always pass cancellation token to OpenAsync for proper cancellation support
                await connection.OpenAsync(cancellationToken);

                // Execute forward operations in sequence, chaining outputs to inputs
                lock (this.lockObject)
                {
                    foreach (var transactionalOperation in this.operations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Fire BeforeCommit event using the proper method
                        transactionalOperation.OnBeforeCommit();

                        var cmd = transactionalOperation.CommitCommand;
                        cmd.Connection = connection;
                        T result = transactionalOperation.Input;
                        switch (transactionalOperation.ExecMode)
                        {
                            case SqlExecMode.ExecuteReader:
                                var reader = cmd.ExecuteReader();
                                result = this.provider.Mapper.MapFromReader(reader);
                                transactionalOperation.Output = result;
                                break;
                            case SqlExecMode.ExecuteNonQuery:
                                cmd.ExecuteNonQuery();
                                break;
                            case SqlExecMode.ExecuteScalar:
                                cmd.ExecuteScalar();
                                break;
                        }

                        // Fire AfterCommit event using the proper method
                        transactionalOperation.OnAfterCommit();

                        reverseOperations.Push((result, transactionalOperation));
                    }
                }

                this.State = TransactionState.Committed;
                return true;
            }
            catch (Exception ex)
            {
                this.State = TransactionState.RollingBack;
                Logger.TransactionFailed(ex);

                // Rollback in reverse order
                var rollbackErrors = new List<Exception>();

                while (reverseOperations.Count > 0)
                {
                    var (result, operation) = reverseOperations.Pop();
                    if (result != null)
                    {
                        operation.Input = result;
                    }

                    try
                    {
                        // Fire BeforeRollback event using the proper method
                        operation.OnBeforeRollback();

                        using var connection = new SQLiteConnection(this.connectionString);
                        await connection.OpenAsync(cancellationToken);
                        var cmd = operation.RollbackCommand;
                        cmd.Connection = connection;

                        switch (operation.ExecMode)
                        {
                            case SqlExecMode.ExecuteReader:
                                var reader = cmd.ExecuteReader();
                                result = this.provider.Mapper.MapFromReader(reader);
                                operation.Output = result;
                                break;
                            case SqlExecMode.ExecuteNonQuery:
                                cmd.ExecuteNonQuery();
                                break;
                            case SqlExecMode.ExecuteScalar:
                                cmd.ExecuteScalar();
                                break;
                        }

                        // Fire AfterRollback event using the proper method
                        operation.OnAfterRollback();
                    }
                    catch (Exception rollbackEx)
                    {
                        rollbackErrors.Add(new Exception(
                            $"Failed to rollback operation {operation.GetType().Name}: {rollbackEx.Message}",
                            rollbackEx));
                    }
                }

                this.State = TransactionState.Failed;

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

            if (this.State == TransactionState.Active && this.operations.Count > 0)
            {
                try
                {
                    // Execute the transaction based on shouldCommit flag
                    if (this.shouldCommit)
                    {
                        // Execute normally (commit)
                        var task = this.ExecuteAsync(CancellationToken.None);
                        task.Wait();
                    }
                    else
                    {
                        // Rollback - fire rollback events for all operations
                        this.State = TransactionState.RollingBack;

                        foreach (var operation in this.operations)
                        {
                            try
                            {
                                // Fire BeforeRollback event using the proper method
                                operation.OnBeforeRollback();

                                if (operation.RollbackCommand != null)
                                {
                                    using var connection = new SQLiteConnection(this.connectionString);
                                    connection.Open();
                                    operation.RollbackCommand.Connection = connection;
                                    operation.RollbackCommand.ExecuteNonQuery();
                                }

                                // Fire AfterRollback event using the proper method
                                operation.OnAfterRollback();
                            }
                            catch (Exception ex)
                            {
                                // Log rollback error but continue with other rollbacks
                                Logger.TransactionFailed(ex);
                            }
                        }

                        this.State = TransactionState.Failed;
                    }
                }
                catch (Exception ex)
                {
                    // Log the error
                    Logger.TransactionFailed(ex);
                    this.State = TransactionState.Failed;
                }
            }

            this.disposed = true;
        }
    }
}