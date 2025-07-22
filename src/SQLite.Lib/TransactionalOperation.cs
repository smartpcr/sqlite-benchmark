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
    public class TransactionalOperation : ITransactionalOperation
    {
        public string OperationId { get; private set; }
        public string Description { get; private set; }
        public SqlExecMode ExecMode { get; private set; }
        public SQLiteCommand CommitCommand { get; private set; }
        public SQLiteCommand RollbackCommand { get; private set; }

        private TransactionalOperation()
        {
        }

        public static TransactionalOperation Create<T, TKey>(
            IPersistenceProvider<T, TKey> persistenceProvider,
            DbOperationType opType,
            T fromValue,
            T toValue = null)
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            var transactionalOperation = new TransactionalOperation();

            transactionalOperation.OperationId = Guid.NewGuid().ToString();
            transactionalOperation.Description = $"{opType} operation for entity type {typeof(T).Name}";
            switch (opType)
            {
                case DbOperationType.Select:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteReader;
                    transactionalOperation.CommitCommand = persistenceProvider.CreateSelectCommand(fromValue.Id, fromValue.Version);
                    transactionalOperation.RollbackCommand = null; // No rollback for select
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
    }
}