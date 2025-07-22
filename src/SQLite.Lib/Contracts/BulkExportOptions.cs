// -----------------------------------------------------------------------
// <copyright file="BulkExportOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;

    /// <summary>
    /// Options for bulk export operations.
    /// </summary>
    public class BulkExportOptions
    {
        public int BatchSize { get; set; } = 1000;
        public bool IncludeDeleted { get; set; } = false;
        public string[] IncludeFields { get; set; }
        public string[] ExcludeFields { get; set; }
        public TimeSpan? Timeout { get; set; }
    }
}