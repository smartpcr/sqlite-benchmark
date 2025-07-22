// -----------------------------------------------------------------------
// <copyright file="AuditFieldType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    /// <summary>
    /// Types of audit fields.
    /// </summary>
    public enum AuditFieldType
    {
        CreatedTime,
        CreatedBy,
        LastWriteTime,
        LastWriteBy,
        Version,
        IsDeleted
    }
}