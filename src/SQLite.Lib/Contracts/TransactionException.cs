// -----------------------------------------------------------------------
// <copyright file="TransactionException.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;

    /// <summary>
    /// Exception thrown when a transaction operation fails.
    /// </summary>
    public class TransactionException : Exception
    {
        public string OperationId { get; }

        public TransactionException(string message, Exception innerException, string operationId)
            : base(message, innerException)
        {
            this.OperationId = operationId;
        }
    }
}