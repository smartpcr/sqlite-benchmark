// -----------------------------------------------------------------------
// <copyright file="AvailabilityType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the availability type of an update.
    /// </summary>
    public enum AvailabilityType
    {
        /// <summary>
        /// Update is available locally.
        /// </summary>
        Local = 0,

        /// <summary>
        /// Update is available online.
        /// </summary>
        Online = 1,

        /// <summary>
        /// Update availability is unknown.
        /// </summary>
        Unknown = 2,

        /// <summary>
        /// Update is available through notification.
        /// </summary>
        Notify = 3
    }
}