// -----------------------------------------------------------------------
// <copyright file="SQLiteDbType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    /// <summary>
    /// Specifies SQLite data types for column mapping.
    /// SQLite uses a dynamic type system with five storage classes.
    /// </summary>
    public enum SQLiteDbType
    {
        /// <summary>
        /// INTEGER - 64-bit signed integer
        /// </summary>
        Integer,

        /// <summary>
        /// REAL - 64-bit floating point number
        /// </summary>
        Real,

        /// <summary>
        /// TEXT - UTF-8, UTF-16BE or UTF-16LE string
        /// </summary>
        Text,

        /// <summary>
        /// BLOB - Binary Large Object
        /// </summary>
        Blob,

        /// <summary>
        /// NUMERIC - May be stored as INTEGER or REAL depending on value
        /// </summary>
        Numeric
    }
}