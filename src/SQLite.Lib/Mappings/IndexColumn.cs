// -----------------------------------------------------------------------
// <copyright file="IndexColumn.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    /// <summary>
    /// Represents a column in an index.
    /// </summary>
    public class IndexColumn
    {
        public string ColumnName { get; set; }
        public int Order { get; set; }
        public bool IsIncluded { get; set; }
    }
}