// -----------------------------------------------------------------------
// <copyright file="UpdateEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a software update entity with metadata and tracking information.
    /// </summary>
    public class UpdateEntity : BaseEntity<string>
    {
        /// <summary>
        /// Gets or sets the display name of the update.
        /// </summary>
        public string UpdateName { get; set; }

        /// <summary>
        /// Gets or sets the version string of the update.
        /// </summary>
        public string UpdateVersion { get; set; }

        /// <summary>
        /// Gets or sets the description of the update.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the publisher of the update.
        /// </summary>
        public string Publisher { get; set; }

        /// <summary>
        /// Gets or sets the release date of the update.
        /// </summary>
        public DateTimeOffset ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the type of the update package (e.g., "MSI", "EXE", "ZIP").
        /// </summary>
        public string PackageType { get; set; }

        /// <summary>
        /// Gets or sets the size of the update package in bytes.
        /// </summary>
        public long PackageSize { get; set; }

        /// <summary>
        /// Gets or sets the download URL for the update package.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the list of dependencies required by this update.
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the prerequisites required before installing this update.
        /// </summary>
        public Dictionary<string, string> Prerequisites { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the current state of the update.
        /// </summary>
        public UpdateState State { get; set; }

        /// <summary>
        /// Gets or sets the priority level of the update.
        /// </summary>
        public UpdatePriority Priority { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateEntity"/> class.
        /// </summary>
        public UpdateEntity()
        {
            this.Dependencies = new List<string>();
            this.Prerequisites = new Dictionary<string, string>();
            this.State = UpdateState.Available;
            this.Priority = UpdatePriority.Normal;
            this.CreatedTime = DateTimeOffset.UtcNow;
            this.LastWriteTime = DateTimeOffset.UtcNow;
            this.Version = 1;
            this.IsDeleted = false;
        }
    }
}
