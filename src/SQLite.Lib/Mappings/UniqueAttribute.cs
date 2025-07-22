// -----------------------------------------------------------------------
// <copyright file="UniqueAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Specifies a unique constraint on a column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UniqueAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the constraint name.
        /// </summary>
        public string Name { get; set; }

        public UniqueAttribute()
        {
        }

        public UniqueAttribute(string name)
        {
            this.Name = name;
        }
    }
}