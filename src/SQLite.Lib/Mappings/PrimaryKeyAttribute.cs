// -----------------------------------------------------------------------
// <copyright file="PrimaryKeyAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Marks a property as the primary key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class PrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether the primary key is auto-incremented.
        /// </summary>
        public bool IsAutoIncrement { get; set; }

        /// <summary>
        /// Gets or sets whether this is part of a composite key.
        /// </summary>
        public bool IsComposite { get; set; }

        /// <summary>
        /// Gets or sets the order in a composite key.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets the sequence name for key generation.
        /// </summary>
        public string SequenceName { get; set; }
    }
}