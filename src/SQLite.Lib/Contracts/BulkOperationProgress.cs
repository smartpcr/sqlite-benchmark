// -----------------------------------------------------------------------
// <copyright file="BulkOperationProgress.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;

    /// <summary>
    /// Progress information for bulk operations.
    /// </summary>
    public class BulkOperationProgress
    {
        public long ProcessedCount { get; set; }
        public long TotalCount { get; set; }
        public double PercentComplete => TotalCount > 0 ? (ProcessedCount * 100.0 / TotalCount) : 0;
        public TimeSpan ElapsedTime { get; set; }
        public string CurrentOperation { get; set; }
    }
}