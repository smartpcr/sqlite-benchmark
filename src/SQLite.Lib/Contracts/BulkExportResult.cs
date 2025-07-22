// -----------------------------------------------------------------------
// <copyright file="BulkExportResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a bulk export operation.
    /// </summary>
    public class BulkExportResult<T>
    {
        public IEnumerable<T> ExportedEntities { get; set; }
        public long ExportedCount { get; set; }
        public TimeSpan Duration { get; set; }
    }
}