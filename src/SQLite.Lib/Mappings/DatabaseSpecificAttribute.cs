// -----------------------------------------------------------------------
// <copyright file="DatabaseSpecificAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Specifies database-specific settings for a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class DatabaseSpecificAttribute : Attribute
    {
        /// <summary>
        /// Gets the database provider name (e.g., "SQLite", "SqlServer").
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Gets or sets provider-specific SQL type.
        /// </summary>
        public string SqlType { get; set; }

        /// <summary>
        /// Gets or sets provider-specific settings as key-value pairs.
        /// </summary>
        public string Settings { get; set; }

        public DatabaseSpecificAttribute(string provider)
        {
            this.Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
    }
}