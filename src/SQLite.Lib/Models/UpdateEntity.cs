// -----------------------------------------------------------------------
// <copyright file="UpdateEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Collections.Generic;
    using SQLite.Lib.Entities;
    using SQLite.Lib.Mappings;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a software update entity with metadata and tracking information.
    /// </summary>
    [Table("UpdateEntity")]
    public class UpdateEntity : BaseEntity<string>
    {
        /// <summary>
        /// Gets or sets the display name of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("DisplayName")]
        [Column("DisplayName", SQLiteDbType.Text, NotNull = true)]
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the version of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("UpdateVersion")]
        [Column("UpdateVersion", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_UpdateEntity_UpdateVersion")]
        public string UpdateVersion { get; set; }

        /// <summary>
        /// Gets or sets the OEM/SBE version of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("SbeVersion")]
        [Column("SbeVersion", SQLiteDbType.Text)]
        public string SbeVersion { get; set; }

        /// <summary>
        /// Gets or sets the description of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("Description")]
        [Column("Description", SQLiteDbType.Text)]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the update state.
        /// </summary>
        [DataMember]
        [JsonProperty("State")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("State", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_UpdateEntity_State")]
        public UpdateState State { get; set; }

        /// <summary>
        /// Gets or sets the publisher of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("Publisher")]
        [Column("Publisher", SQLiteDbType.Text)]
        public string Publisher { get; set; }

        /// <summary>
        /// Gets or sets the date the update was installed.
        /// </summary>
        [DataMember]
        [JsonProperty("InstalledDate")]
        [Column("InstalledDate", SQLiteDbType.Text)]
        public DateTime? InstalledDate { get; set; }

        /// <summary>
        /// Gets or sets the KB article link of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("KbLink")]
        [Column("KbLink", SQLiteDbType.Text)]
        public string KbLink { get; set; }

        /// <summary>
        /// Gets or sets the release link of the update.
        /// </summary>
        [DataMember]
        [JsonProperty("ReleaseLink")]
        [Column("ReleaseLink", SQLiteDbType.Text)]
        public string ReleaseLink { get; set; }

        /// <summary>
        /// Gets or sets the minimum required version to apply the update.
        /// </summary>
        [DataMember]
        [JsonProperty("MinVersionRequired")]
        [Column("MinVersionRequired", SQLiteDbType.Text)]
        public string MinVersionRequired { get; set; }

        /// <summary>
        /// Gets or sets the minimum required OEM version to apply the update.
        /// </summary>
        [DataMember]
        [JsonProperty("MinSbeVersionRequired")]
        [Column("MinSbeVersionRequired", SQLiteDbType.Text)]
        public string MinSbeVersionRequired { get; set; }

        /// <summary>
        /// Gets or sets the directory path of the update package.
        /// </summary>
        [DataMember]
        [JsonProperty("PackagePath")]
        [Column("PackagePath", SQLiteDbType.Text)]
        public string PackagePath { get; set; }

        /// <summary>
        /// Gets or sets the size of the update package in MB.
        /// </summary>
        [DataMember]
        [JsonProperty("PackageSizeInMb")]
        [Column("PackageSizeInMb", SQLiteDbType.Integer, NotNull = true)]
        public uint PackageSizeInMb { get; set; }

        /// <summary>
        /// Gets or sets the type of the update package.
        /// </summary>
        [DataMember]
        [JsonProperty("PackageType")]
        [Column("PackageType", SQLiteDbType.Text)]
        public string PackageType { get; set; }

        /// <summary>
        /// Gets or sets the delivery type.
        /// </summary>
        [DataMember]
        [JsonProperty("DeliveryType")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("DeliveryType", SQLiteDbType.Text)]
        public DeliveryType DeliveryType { get; set; }

        /// <summary>
        /// Gets or sets the reboot requirement type.
        /// </summary>
        [DataMember]
        [JsonProperty("RebootRequired")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("RebootRequired", SQLiteDbType.Text)]
        public RebootRequirement RebootRequired { get; set; }

        /// <summary>
        /// Gets or sets the install type.
        /// </summary>
        [DataMember]
        [JsonProperty("InstallType")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("InstallType", SQLiteDbType.Text)]
        public InstallType InstallType { get; set; }

        /// <summary>
        /// Gets or sets the update availability type.
        /// </summary>
        [DataMember]
        [JsonProperty("AvailabilityType")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("AvailabilityType", SQLiteDbType.Text)]
        public AvailabilityType AvailabilityType { get; set; }

        /// <summary>
        /// Gets or sets the aggregated state of update prechecks.
        /// </summary>
        [DataMember]
        [JsonProperty("HealthState")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("HealthState", SQLiteDbType.Text)]
        public HealthState HealthState { get; set; }

        /// <summary>
        /// Gets or sets the last time an update precheck was completed.
        /// </summary>
        [DataMember]
        [JsonProperty("HealthCheckDate")]
        [Column("HealthCheckDate", SQLiteDbType.Text)]
        public DateTime? HealthCheckDate { get; set; }

        /// <summary>
        /// Gets or sets whether the update is recalled.
        /// </summary>
        [DataMember]
        [JsonProperty("IsRecalled")]
        [Column("IsRecalled", SQLiteDbType.Integer, NotNull = true)]
        public bool IsRecalled { get; set; }

        /// <summary>
        /// Gets or sets the OEM family.
        /// </summary>
        [DataMember]
        [JsonProperty("OemFamily")]
        [Column("OemFamily", SQLiteDbType.Text)]
        public string OemFamily { get; set; }

        /// <summary>
        /// Gets or sets the prerequisites as a JSON string.
        /// </summary>
        [DataMember]
        [JsonProperty("Prerequisites")]
        [Column("Prerequisites", SQLiteDbType.Text)]
        public string Prerequisites { get; set; }

        /// <summary>
        /// Gets or sets the additional properties as a JSON string.
        /// </summary>
        [DataMember]
        [JsonProperty("AdditionalProperties")]
        [Column("AdditionalProperties", SQLiteDbType.Text)]
        public string AdditionalProperties { get; set; }

        /// <summary>
        /// Gets or sets the bill of materials as a JSON string.
        /// </summary>
        [DataMember]
        [JsonProperty("BillOfMaterials")]
        [Column("BillOfMaterials", SQLiteDbType.Text)]
        public string BillOfMaterials { get; set; }

        /// <summary>
        /// Gets or sets the health check result as a JSON string.
        /// </summary>
        [DataMember]
        [JsonProperty("HealthCheckResult")]
        [Column("HealthCheckResult", SQLiteDbType.Text)]
        public string HealthCheckResult { get; set; }

        /// <summary>
        /// Gets or sets the update state properties as a JSON string.
        /// </summary>
        [DataMember]
        [JsonProperty("UpdateStateProperties")]
        [Column("UpdateStateProperties", SQLiteDbType.Text)]
        public string UpdateStateProperties { get; set; }

        /// <summary>
        /// Gets or sets whether scan and download should be deferred.
        /// </summary>
        [DataMember]
        [JsonProperty("DeferScanAndDownload")]
        [Column("DeferScanAndDownload", SQLiteDbType.Integer, NotNull = true)]
        public bool DeferScanAndDownload { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateEntity"/> class.
        /// </summary>
        public UpdateEntity()
        {
            this.State = UpdateState.Ready;
            this.CreatedTime = DateTimeOffset.UtcNow;
            this.LastWriteTime = DateTimeOffset.UtcNow;
            this.IsDeleted = false;
            this.IsRecalled = false;
            this.DeferScanAndDownload = false;
            this.PackageSizeInMb = 0;
        }
    }
}
