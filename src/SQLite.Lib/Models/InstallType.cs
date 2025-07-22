// -----------------------------------------------------------------------
// <copyright file="InstallType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the type of installation for an update.
    /// </summary>
    public enum InstallType
    {
        /// <summary>
        /// Full installation.
        /// </summary>
        Full = 0,

        /// <summary>
        /// Hotfix installation.
        /// </summary>
        Hotfix = 1
    }
}