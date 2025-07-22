// -----------------------------------------------------------------------
// <copyright file="ITransactionalOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Data.SQLite;

    /// <summary>
    /// Delegate for BeforeCommit event.
    /// </summary>
    public delegate void BeforeCommitEventHandler<TInput, TOutput>(ITransactionalOperation<TInput, TOutput> sender, TInput input);

    /// <summary>
    /// Delegate for AfterCommit event.
    /// </summary>
    public delegate void AfterCommitEventHandler<TInput, TOutput>(ITransactionalOperation<TInput, TOutput> sender, TOutput output);

    /// <summary>
    /// Delegate for BeforeRollback event.
    /// </summary>
    public delegate void BeforeRollbackEventHandler<TInput, TOutput>(ITransactionalOperation<TInput, TOutput> sender, TOutput output);

    /// <summary>
    /// Delegate for AfterRollback event.
    /// </summary>
    public delegate void AfterRollbackEventHandler<TInput, TOutput>(ITransactionalOperation<TInput, TOutput> sender, TInput input);

    /// <summary>
    /// Defines a transactional operation that can be committed or rolled back.
    /// Generic version supports entity-specific operations with type safety.
    /// </summary>
    public interface ITransactionalOperation<TInput, TOutput>
    {
        /// <summary>
        /// Gets the unique identifier for this operation.
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// Gets the operation description for logging.
        /// </summary>
        string Description { get; }

        SqlExecMode ExecMode { get; }

        TInput Input { get; set; }

        TOutput Output { get; set; }

        /// <summary>
        /// Event raised before the commit operation is executed.
        /// </summary>
        event BeforeCommitEventHandler<TInput, TOutput> BeforeCommit;

        /// <summary>
        /// Event raised after the commit operation has completed successfully.
        /// </summary>
        event AfterCommitEventHandler<TInput, TOutput> AfterCommit;

        /// <summary>
        /// Event raised before the rollback operation is executed.
        /// </summary>
        event BeforeRollbackEventHandler<TInput, TOutput> BeforeRollback;

        /// <summary>
        /// Event raised after the rollback operation has completed successfully.
        /// </summary>
        event AfterRollbackEventHandler<TInput, TOutput> AfterRollback;

        SQLiteCommand CommitCommand { get; }

        SQLiteCommand RollbackCommand { get; }

        /// <summary>
        /// Raises the BeforeCommit event.
        /// </summary>
        void OnBeforeCommit();

        /// <summary>
        /// Raises the AfterCommit event.
        /// </summary>
        void OnAfterCommit();

        /// <summary>
        /// Raises the BeforeRollback event.
        /// </summary>
        void OnBeforeRollback();

        /// <summary>
        /// Raises the AfterRollback event.
        /// </summary>
        void OnAfterRollback();
    }
}