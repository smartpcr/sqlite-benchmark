// -----------------------------------------------------------------------
// <copyright file="BulkImportResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a bulk import operation.
    /// </summary>
    public class BulkImportResult
    {
        public long SuccessCount { get; set; }
        public long FailureCount { get; set; }
        public long DuplicateCount { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}