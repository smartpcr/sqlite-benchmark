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
        /// Update cannot be installed because it has some prerequisite.
        /// </summary>
        HasPrerequisite = 0,

        /// <summary>
        /// Update cannot be installed because it is obsolete, it has no run.
        /// </summary>
        Obsolete = 1,

        /// <summary>
        /// Update is applicable.
        /// </summary>
        Ready = 2,

        /// <summary>
        /// Update is not applicable because another update is in progress.
        /// </summary>
        NotApplicableBecauseAnotherUpdateIsInProgress = 3,

        /// <summary>
        /// The update is being downloaded to the infra share or is being extracted.
        /// </summary>
        Preparing = 4,

        /// <summary>
        /// Update is being installed.
        /// </summary>
        Installing = 5,

        /// <summary>
        /// Update has already been installed successfully.
        /// </summary>
        Installed = 6,

        /// <summary>
        /// Download or extraction failed.
        /// </summary>
        PreparationFailed = 7,

        /// <summary>
        /// Update has not been installed successfully.
        /// </summary>
        InstallationFailed = 8,

        /// <summary>
        /// Update is invalid for the stamp.
        /// </summary>
        Invalid = 9,

        /// <summary>
        /// Update is recalled.
        /// </summary>
        Recalled = 10,

        /// <summary>
        /// Update is downloading.
        /// </summary>
        Downloading = 11,

        /// <summary>
        /// Update download failed.
        /// </summary>
        DownloadFailed = 12,

        /// <summary>
        /// Update is running health check.
        /// </summary>
        HealthChecking = 13,

        /// <summary>
        /// Update health check failed.
        /// </summary>
        HealthCheckFailed = 14,

        /// <summary>
        /// Update is ready to install.
        /// </summary>
        ReadyToInstall = 15,

        /// <summary>
        /// Additional update content is required before the update can be ready.
        /// </summary>
        AdditionalContentRequired = 16
    }
}
