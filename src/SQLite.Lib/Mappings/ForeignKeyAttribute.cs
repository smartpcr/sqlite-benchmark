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
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets the referenced table name.
        /// </summary>
        public string ReferencedTable { get; }

        /// <summary>
        /// Gets or sets the referenced column name. Default is "Id".
        /// </summary>
        public string ReferencedColumn { get; set; } = "Id";

        /// <summary>
        /// Gets or sets the constraint name.
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

        public ForeignKeyAttribute(string referencedTable)
        {
            this.ReferencedTable = referencedTable ?? throw new ArgumentNullException(nameof(referencedTable));
        }
    }
}