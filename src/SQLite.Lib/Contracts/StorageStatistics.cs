// -----------------------------------------------------------------------
// <copyright file="StorageStatistics.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Storage statistics for monitoring.
    /// </summary>
    public class StorageStatistics
    {
        public long TotalEntities { get; set; }
        public long ActiveEntities { get; set; }
        public long DeletedEntities { get; set; }
        public long ExpiredEntities { get; set; }
        public long StorageSizeBytes { get; set; }
        public Dictionary<string, long> EntitiesByType { get; set; }
        public DateTimeOffset LastOptimized { get; set; }
    }
}