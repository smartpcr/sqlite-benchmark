// -----------------------------------------------------------------------
// <copyright file="ISQLiteEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.Linq.Expressions;
    using System.Reflection;
    using SQLite.Lib.Mappings;

    /// <summary>
    /// Defines the contract for mapping entities to SQLite tables.
    /// </summary>
    public interface ISQLiteEntityMapper<T, TKey>
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        string GetTableName();
        IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetPropertyMappings();
        string GetPrimaryKeyColumn();
        List<string> GetSelectColumns();
        List<string> GetInsertColumns();
        List<string> GetUpdateColumns();
        void AddParameters(SQLiteCommand command, T entity);
        T MapFromReader(IDataReader reader);
        string GenerateCreateTableSql(bool includeIfNotExists = true);
        IEnumerable<string> GenerateCreateIndexSql();
        string GenerateWhereClause(Expression<Func<T, bool>> predicate);
        byte[] SerializeEntity(T entity);
        string SerializeKey(TKey key);
        TKey DeserializeKey(string serialized);
        SQLiteCommand CreateCommand(DbOperationType operationType, TKey key, T fromValue, T toValue);
    }
}