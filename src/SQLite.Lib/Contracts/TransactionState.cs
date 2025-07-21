// -----------------------------------------------------------------------
// <copyright file="TransactionState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    /// <summary>
    /// Transaction states.
    /// </summary>
    public enum TransactionState
    {
        Active,
        Committing,
        Committed,
        RollingBack,
        RolledBack,
        Failed
    }
}