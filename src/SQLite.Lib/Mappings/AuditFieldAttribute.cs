// -----------------------------------------------------------------------
// <copyright file="AuditFieldAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;

    /// <summary>
    /// Marks a property as an audit field with automatic management.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AuditFieldAttribute : Attribute
    {
        /// <summary>
        /// Gets the type of audit field.
        /// </summary>
        public AuditFieldType FieldType { get; }

        public AuditFieldAttribute(AuditFieldType fieldType)
        {
            this.FieldType = fieldType;
        }
    }
}