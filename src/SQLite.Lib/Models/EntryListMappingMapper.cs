// -----------------------------------------------------------------------
// <copyright file="EntryListMappingMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Data.SQLite;
    using SQLite.Lib.Mappings;

    /// <summary>
    /// Provides mapping functionality for EntryListMapping entities.
    /// </summary>
    public class EntryListMappingMapper : BaseEntityMapper<EntryListMapping, string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntryListMappingMapper"/> class.
        /// </summary>
        public EntryListMappingMapper() : base()
        {
        }

        /// <summary>
        /// Creates a SELECT command for retrieving an EntryListMapping by list and entry cache keys.
        /// </summary>
        /// <param name="listCacheKey">The list cache key.</param>
        /// <param name="entryCacheKey">The entry cache key.</param>
        /// <returns>The SQL command.</returns>
        public SQLiteCommand CreateSelectByKeysCommand(string listCacheKey, string entryCacheKey)
        {
            var sql = $"SELECT * FROM {this.GetTableName()} WHERE ListCacheKey = @ListCacheKey AND EntryCacheKey = @EntryCacheKey";
            var cmd = new SQLiteCommand(sql);
            cmd.Parameters.AddWithValue("@ListCacheKey", listCacheKey);
            cmd.Parameters.AddWithValue("@EntryCacheKey", entryCacheKey);
            return cmd;
        }

        /// <summary>
        /// Creates a SELECT command for retrieving all entries for a list cache key.
        /// </summary>
        /// <param name="listCacheKey">The list cache key.</param>
        /// <returns>The SQL command.</returns>
        public SQLiteCommand CreateSelectByListKeyCommand(string listCacheKey)
        {
            var sql = $"SELECT * FROM {this.GetTableName()} WHERE ListCacheKey = @ListCacheKey ORDER BY EntryCacheKey";
            var cmd = new SQLiteCommand(sql);
            cmd.Parameters.AddWithValue("@ListCacheKey", listCacheKey);
            return cmd;
        }

        /// <summary>
        /// Creates a DELETE command for removing all entries for a list cache key.
        /// </summary>
        /// <param name="listCacheKey">The list cache key.</param>
        /// <returns>The SQL command.</returns>
        public SQLiteCommand CreateDeleteByListKeyCommand(string listCacheKey)
        {
            var sql = $"DELETE FROM {this.GetTableName()} WHERE ListCacheKey = @ListCacheKey";
            var cmd = new SQLiteCommand(sql);
            cmd.Parameters.AddWithValue("@ListCacheKey", listCacheKey);
            return cmd;
        }
    }
}