// -----------------------------------------------------------------------
// <copyright file="HealthState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the health state of an update.
    /// </summary>
    public enum HealthState
    {
        /// <summary>
        /// Health state is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Health state is success.
        /// </summary>
        Success = 1,

        /// <summary>
        /// Health state is failure.
        /// </summary>
        Failure = 2,

        /// <summary>
        /// Health state indicates a warning.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Health state is in progress.
        /// </summary>
        InProgress = 4
    }
}