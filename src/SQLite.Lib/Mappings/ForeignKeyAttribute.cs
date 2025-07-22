// -----------------------------------------------------------------------
// <copyright file="ForeignKeyAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Defines a foreign key relationship.
    /// For composite foreign keys, apply to each participating property with the same constraint name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ForeignKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets the referenced table name.
        /// </summary>
        public string ReferencedTable { get; }

        /// <summary>
        /// Gets or sets the referenced column name. 
        /// For single column FK, defaults to "Id".
        /// For composite FK, specify the corresponding column in the referenced table.
        /// </summary>
        public string ReferencedColumn { get; set; } = "Id";

        /// <summary>
        /// Gets or sets the constraint name.
        /// For composite keys, use the same name on all participating properties.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ON DELETE behavior.
        /// </summary>
        public string OnDelete { get; set; } = "NO ACTION";

        /// <summary>
        /// Gets or sets the ON UPDATE behavior.
        /// </summary>
        public string OnUpdate { get; set; } = "NO ACTION";

        /// <summary>
        /// Gets or sets the ordinal position for composite foreign keys.
        /// Must match the order of columns in the referenced table's primary key.
        /// </summary>
        public int Ordinal { get; set; } = 0;

        public ForeignKeyAttribute(string referencedTable)
        {
            this.ReferencedTable = referencedTable ?? throw new ArgumentNullException(nameof(referencedTable));
        }
        
        public ForeignKeyAttribute(string referencedTable, string referencedColumn) : this(referencedTable)
        {
            this.ReferencedColumn = referencedColumn;
        }
    }
}