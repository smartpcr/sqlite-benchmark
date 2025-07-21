// -----------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Runtime.InteropServices;
    using SQLite.Lib.Contracts;

    public abstract class BaseEntity<TKey> : IEntity<TKey> where TKey : IEquatable<TKey>
    {
        public TKey Id { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset LastWriteTime { get; set; }
        public long Version { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset? ExpirationTime { get; set; }
        public long EstimateEntitySize()
        {
            return MemorySizeEstimator.EstimateObjectSize(this);
        }
    }
}