// -----------------------------------------------------------------------
// <copyright file="ForeignKeyDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    /// <summary>
    /// Represents a foreign key definition.
    /// </summary>
    public class ForeignKeyDefinition
    {
        public string ConstraintName { get; set; }
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
        public string OnDelete { get; set; }
        public string OnUpdate { get; set; }
    }
}