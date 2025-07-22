// -----------------------------------------------------------------------
// <copyright file="ComputedAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Marks a property as a computed column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ComputedAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the computation expression.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Gets or sets whether the computed value is persisted.
        /// </summary>
        public bool IsPersisted { get; set; }

        public ComputedAttribute()
        {
        }

        public ComputedAttribute(string expression)
        {
            this.Expression = expression;
        }
    }
}