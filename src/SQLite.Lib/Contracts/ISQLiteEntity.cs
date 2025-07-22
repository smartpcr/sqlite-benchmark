// -----------------------------------------------------------------------
// <copyright file="ISQLiteEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;

    public interface ISQLiteEntity<TKey> : IEntity<TKey> where TKey : IEquatable<TKey>
    {
        string TableName { get; }
    }
}
