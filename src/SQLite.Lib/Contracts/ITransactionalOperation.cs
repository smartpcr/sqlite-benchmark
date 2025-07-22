// -----------------------------------------------------------------------
// <copyright file="ITransactionalOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System.Data.SqlClient;

    /// <summary>
    /// Defines a transactional operation that can be committed or rolled back.
    /// Generic version supports entity-specific operations with type safety.
    /// </summary>
    public interface ITransactionalOperation
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

        SqlCommand CommitCommand { get; }

        SqlCommand RollbackCommand { get; }
    }
}