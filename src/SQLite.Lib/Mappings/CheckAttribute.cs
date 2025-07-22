// -----------------------------------------------------------------------
// <copyright file="CheckAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Specifies a check constraint on a column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class CheckAttribute : Attribute
    {
        /// <summary>
        /// Gets the check constraint expression.
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// Gets or sets the constraint name.
        /// </summary>
        public string Name { get; set; }

        public CheckAttribute(string expression)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }
}