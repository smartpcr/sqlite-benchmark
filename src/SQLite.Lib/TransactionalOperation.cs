// -----------------------------------------------------------------------
// <copyright file="TransactionalOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------


namespace SQLite.Lib
{
    using System;
    using System.Data.SQLite;
    using SQLite.Lib.Contracts;

    /// <summary>
    /// Concrete implementation of a transactional operation
    /// </summary>
    public class TransactionalOperation<TInput, TOutput> : ITransactionalOperation<TInput, TOutput>
    {
        public string OperationId { get; private set; }
        public string Description { get; private set; }
        public SqlExecMode ExecMode { get; private set; }
        public TInput Input { get; set; }
        public TOutput Output { get; set; }
        public SQLiteCommand CommitCommand { get; private set; }
        public SQLiteCommand RollbackCommand { get; private set; }

        public event BeforeCommitEventHandler<TInput, TOutput> BeforeCommit;
        public event AfterCommitEventHandler<TInput, TOutput> AfterCommit;
        public event BeforeRollbackEventHandler<TInput, TOutput> BeforeRollback;
        public event AfterRollbackEventHandler<TInput, TOutput> AfterRollback;

        private TransactionalOperation()
        {
        }

        public static TransactionalOperation<T, T> Create<T, TKey>(
            IPersistenceProvider<T, TKey> persistenceProvider,
            DbOperationType opType,
            T fromValue,
            T toValue = null)
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            var transactionalOperation = new TransactionalOperation<T, T>();
            transactionalOperation.Input = fromValue;
            transactionalOperation.Output = toValue;

            transactionalOperation.OperationId = Guid.NewGuid().ToString();
            transactionalOperation.Description = $"{opType} operation for entity type {typeof(T).Name}";
            switch (opType)
            {
                case DbOperationType.Select:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteReader;
                    transactionalOperation.CommitCommand = persistenceProvider.CreateSelectCommand(fromValue.Id);
                    transactionalOperation.RollbackCommand = null; // No rollback for select
                    transactionalOperation.AfterCommit += (_, output) =>
                    {
                        transactionalOperation.Output = output;
                    };
                    break;
                case DbOperationType.Insert:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteNonQuery;
                    transactionalOperation.CommitCommand = persistenceProvider.CreateInsertCommand(fromValue);
                    transactionalOperation.RollbackCommand = persistenceProvider.CreateDeleteCommand(fromValue.Id);
                    break;
                case DbOperationType.Update:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteNonQuery;
                    transactionalOperation.CommitCommand = persistenceProvider.CreateUpdateCommand(fromValue, toValue);
                    transactionalOperation.RollbackCommand = persistenceProvider.CreateUpdateCommand(toValue, fromValue);
                    break;
                case DbOperationType.Delete:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteNonQuery;
                    transactionalOperation.CommitCommand = persistenceProvider.CreateDeleteCommand(fromValue.Id);
                    transactionalOperation.RollbackCommand = persistenceProvider.CreateInsertCommand(fromValue);
                    break;
            }

            return transactionalOperation;
        }

        /// <summary>
        /// Raises the BeforeCommit event.
        /// </summary>
        public virtual void OnBeforeCommit()
        {
            this.BeforeCommit?.Invoke(this, this.Input);
        }

        /// <summary>
        /// Raises the AfterCommit event.
        /// </summary>
        public virtual void OnAfterCommit()
        {
            this.AfterCommit?.Invoke(this, this.Output);
        }

        /// <summary>
        /// Raises the BeforeRollback event.
        /// </summary>
        public virtual void OnBeforeRollback()
        {
            this.BeforeRollback?.Invoke(this, this.Output);
        }

        /// <summary>
        /// Raises the AfterRollback event.
        /// </summary>
        public virtual void OnAfterRollback()
        {
            this.AfterRollback?.Invoke(this, this.Input);
        }
    }
}