// -----------------------------------------------------------------------
// <copyright file="ISQLiteEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using SQLite.Lib.Contracts;

    /// <summary>
    /// Defines the contract for mapping entities to SQLite tables.
    /// </summary>
    public interface ISQLiteEntityMapper<T, TKey>
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        string GetTableName();
        string GetPrimaryKeyColumn();
        List<string> GetSelectColumns();
        List<string> GetInsertColumns();
        List<string> GetUpdateColumns();
        void AddParameters(SQLiteCommand command, T entity);
        T MapFromReader(IDataReader reader);
        byte[] SerializeEntity(T entity);
        string SerializeKey(TKey key);
        TKey DeserializeKey(string serialized);
    }
}