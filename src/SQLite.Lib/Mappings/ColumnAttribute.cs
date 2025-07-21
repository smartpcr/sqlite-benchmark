// -----------------------------------------------------------------------
// <copyright file="ColumnAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;
    using System.Data;

    /// <summary>
    /// Maps a property to a database column with specific settings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the column name. If not specified, property name is used.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the SQL data type.
        /// </summary>
        public SqlDbType? SqlType { get; set; }

        /// <summary>
        /// Gets or sets the size/length of the column. -1 indicates MAX.
        /// </summary>
        public int Size { get; set; } = 0;

        /// <summary>
        /// Gets or sets the precision for numeric columns.
        /// </summary>
        public int Precision { get; set; } = 0;

        /// <summary>
        /// Gets or sets the scale for numeric columns.
        /// </summary>
        public int Scale { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether the column allows NULL values.
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// Gets or sets the default value for the column.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the name of the default constraint.
        /// </summary>
        public string DefaultConstraintName { get; set; }

        /// <summary>
        /// Gets or sets the column order in the table.
        /// </summary>
        public int Order { get; set; } = -1;

        public ColumnAttribute()
        {
        }

        public ColumnAttribute(string name)
        {
            this.Name = name;
        }

        public ColumnAttribute(string name, SqlDbType sqlType) : this(name)
        {
            this.SqlType = sqlType;
        }
    }
}