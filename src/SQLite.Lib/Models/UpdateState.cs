// -----------------------------------------------------------------------
// <copyright file="UpdateState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the current state of an update.
    /// </summary>
    public enum UpdateState
    {
        /// <summary>
        /// Update is pending installation.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Update is currently being downloaded.
        /// </summary>
        Downloading = 1,

        /// <summary>
        /// Update has been downloaded and is ready for installation.
        /// </summary>
        Downloaded = 2,

        /// <summary>
        /// Update is currently being installed.
        /// </summary>
        Installing = 3,

        /// <summary>
        /// Update has been successfully installed.
        /// </summary>
        Installed = 4,

        /// <summary>
        /// Update installation failed.
        /// </summary>
        Failed = 5,

        /// <summary>
        /// Update has been cancelled.
        /// </summary>
        Cancelled = 6,

        /// <summary>
        /// Update is available but not yet downloaded.
        /// </summary>
        Available = 7
    }
}
