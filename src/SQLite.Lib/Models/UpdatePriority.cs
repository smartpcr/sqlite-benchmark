// -----------------------------------------------------------------------
// <copyright file="UpdatePriority.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the priority level of an update.
    /// </summary>
    public enum UpdatePriority
    {
        /// <summary>
        /// Low priority update that can be deferred.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority update.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority update that should be installed soon.
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical security or stability update that requires immediate attention.
        /// </summary>
        Critical = 3,

        /// <summary>
        /// Emergency update that must be installed immediately.
        /// </summary>
        Emergency = 4
    }
}
