// -----------------------------------------------------------------------
// <copyright file="UpdateRunState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// State of update run.
    /// </summary>
    public enum UpdateRunState
    {
        /// <summary>
        /// Update run state is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Update run is successful.
        /// </summary>
        Succeeded = 1,

        /// <summary>
        /// Update run is in progress.
        /// </summary>
        InProgress = 2,

        /// <summary>
        /// Update run failed.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Health check succeeded.
        /// </summary>
        HealthCheckSucceeded = 4
    }
}