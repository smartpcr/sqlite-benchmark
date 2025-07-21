// -----------------------------------------------------------------------
// <copyright file="IndexAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Marks a property for database indexing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class IndexAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the index name. If not specified, a default name is generated.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the column order in a composite index.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether this is a unique index.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets whether this is a clustered index.
        /// </summary>
        public bool IsClustered { get; set; }

        /// <summary>
        /// Gets or sets whether this column is included (not part of key).
        /// </summary>
        public bool IsIncluded { get; set; }

        /// <summary>
        /// Gets or sets the filter expression for a filtered index.
        /// </summary>
        public string Filter { get; set; }

        public IndexAttribute()
        {
        }

        public IndexAttribute(string name)
        {
            this.Name = name;
        }
    }
}