// -----------------------------------------------------------------------
// <copyright file="PropertyMapping.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;
    using System.Data;
    using System.Reflection;

    /// <summary>
    /// Represents a mapping between a C# property and a database column.
    /// </summary>
    public class PropertyMapping
    {
        public PropertyInfo PropertyInfo { get; set; }
        public string PropertyName { get; set; }
        public Type PropertyType { get; set; }
        public string ColumnName { get; set; }
        public SqlDbType SqlType { get; set; }
        public int? Size { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public object DefaultValue { get; set; }
        public string DefaultConstraintName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsAutoIncrement { get; set; }
        public string SequenceName { get; set; }
        public bool IsUnique { get; set; }
        public string UniqueConstraintName { get; set; }
        public bool IsComputed { get; set; }
        public string ComputedExpression { get; set; }
        public bool IsPersisted { get; set; }
        public bool IsNotMapped { get; set; }
        public bool IsAuditField { get; set; }
        public AuditFieldType? AuditFieldType { get; set; }
        public string CheckConstraint { get; set; }
        public string CheckConstraintName { get; set; }
    }
}