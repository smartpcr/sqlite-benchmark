// -----------------------------------------------------------------------
// <copyright file="TableAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Specifies the database table name and schema for an entity class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the schema name. Default is null since SQLite does not support schema.
        /// For other DB provider that supports schema, it's default to "dbo".
        /// </summary>
        public string Schema { get; set; } = null;

        public TableAttribute(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}