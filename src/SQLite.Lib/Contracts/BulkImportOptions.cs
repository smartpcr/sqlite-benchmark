// -----------------------------------------------------------------------
// <copyright file="BulkImportOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;

    /// <summary>
    /// Options for bulk import operations.
    /// </summary>
    public class BulkImportOptions
    {
        public int BatchSize { get; set; } = 1000;
        public bool IgnoreDuplicates { get; set; } = false;
        public bool ValidateBeforeImport { get; set; } = true;
        public bool UpdateExisting { get; set; } = false;
        public TimeSpan? Timeout { get; set; }
    }
}