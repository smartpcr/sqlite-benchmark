// -----------------------------------------------------------------------
// <copyright file="RebootRequirement.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the reboot requirement after installing an update.
    /// </summary>
    public enum RebootRequirement
    {
        /// <summary>
        /// No reboot is required.
        /// </summary>
        None = 0,

        /// <summary>
        /// Reboot may be required.
        /// </summary>
        Maybe = 1,

        /// <summary>
        /// Reboot is required.
        /// </summary>
        Required = 2
    }
}