// -----------------------------------------------------------------------
// <copyright file="ISQLiteEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using SQLite.Lib.Contracts;

    public interface ISQLiteEntity<TKey> : IEntity<TKey> where TKey : IEquatable<TKey>
    {
        string TableName { get; }
    }
}
