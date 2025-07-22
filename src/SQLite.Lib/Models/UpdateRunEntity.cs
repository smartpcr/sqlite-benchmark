// -----------------------------------------------------------------------
// <copyright file="UpdateRunEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using SQLite.Lib.Entities;
    using SQLite.Lib.Mappings;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents an update run entity that tracks the execution of an update.
    /// </summary>
    [Table("UpdateRunEntity")]
    public class UpdateRunEntity : BaseEntity<string>
    {
        /// <summary>
        /// Gets or sets the name of the update run.
        /// </summary>
        [DataMember]
        [JsonProperty("Name")]
        [Column("Name", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_UpdateRunEntity_Name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the resource ID of the update run.
        /// </summary>
        [DataMember]
        [JsonProperty("ResourceId")]
        [Column("ResourceId", SQLiteDbType.Text, NotNull = true)]
        public string ResourceId { get; set; }

        /// <summary>
        /// Gets or sets the parent resource ID.
        /// </summary>
        [DataMember]
        [JsonProperty("ParentId")]
        [Column("ParentId", SQLiteDbType.Text)]
        public string ParentId { get; set; }

        /// <summary>
        /// Gets or sets the resource type.
        /// </summary>
        [DataMember]
        [JsonProperty("ResourceType")]
        [Column("ResourceType", SQLiteDbType.Text, NotNull = true)]
        public string ResourceType { get; set; }

        /// <summary>
        /// Gets or sets the time when the update run started.
        /// </summary>
        [DataMember]
        [JsonProperty("TimeStarted")]
        [Column("TimeStarted", SQLiteDbType.Text)]
        [Index("IX_UpdateRunEntity_TimeStarted")]
        public DateTime? TimeStarted { get; set; }

        /// <summary>
        /// Gets or sets the completion time of the last completed step, if any.
        /// </summary>
        [DataMember]
        [JsonProperty("LastUpdatedTime")]
        [Column("LastUpdatedTime", SQLiteDbType.Text)]
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// Gets or sets the duration of the update run in seconds.
        /// </summary>
        [DataMember]
        [JsonProperty("Duration")]
        [Column("Duration", SQLiteDbType.Integer, NotNull = true)]
        public long Duration { get; set; }

        /// <summary>
        /// Gets or sets the state of update run.
        /// </summary>
        [DataMember]
        [JsonProperty("State")]
        [JsonConverter(typeof(StringEnumConverter))]
        [Column("State", SQLiteDbType.Text, NotNull = true)]
        [Index("IX_UpdateRunEntity_State")]
        public UpdateRunState State { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the on complete action succeeded.
        /// </summary>
        [DataMember]
        [JsonProperty("OnCompleteActionSuccess")]
        [Column("OnCompleteActionSuccess", SQLiteDbType.Integer, NotNull = true)]
        public bool OnCompleteActionSuccess { get; set; }

        /// <summary>
        /// Gets or sets the preparation download percentage.
        /// </summary>
        [DataMember]
        [JsonProperty("PreparationDownloadPercentage")]
        [Column("PreparationDownloadPercentage", SQLiteDbType.Integer, NotNull = true)]
        public int PreparationDownloadPercentage { get; set; }

        /// <summary>
        /// Gets or sets whether the update run is for preparation.
        /// </summary>
        [DataMember]
        [JsonProperty("IsPreparationRun")]
        [Column("IsPreparationRun", SQLiteDbType.Integer, NotNull = true)]
        public bool IsPreparationRun { get; set; }

        /// <summary>
        /// Gets or sets the progress of the update run as a JSON string.
        /// </summary>
        [DataMember]
        [JsonProperty("Progress")]
        [Column("Progress", SQLiteDbType.Text)]
        public string Progress { get; set; }

        /// <summary>
        /// Gets or sets the current step status.
        /// </summary>
        [DataMember]
        [JsonProperty("ProgressStatus")]
        [Column("ProgressStatus", SQLiteDbType.Text)]
        public string ProgressStatus { get; set; }

        /// <summary>
        /// Gets or sets the current step description.
        /// </summary>
        [DataMember]
        [JsonProperty("ProgressDescription")]
        [Column("ProgressDescription", SQLiteDbType.Text)]
        public string ProgressDescription { get; set; }

        /// <summary>
        /// Gets or sets the current step name.
        /// </summary>
        [DataMember]
        [JsonProperty("CurrentStepName")]
        [Column("CurrentStepName", SQLiteDbType.Text)]
        public string CurrentStepName { get; set; }

        /// <summary>
        /// Gets or sets the total number of steps.
        /// </summary>
        [DataMember]
        [JsonProperty("TotalSteps")]
        [Column("TotalSteps", SQLiteDbType.Integer, NotNull = true)]
        public int TotalSteps { get; set; }

        /// <summary>
        /// Gets or sets the number of completed steps.
        /// </summary>
        [DataMember]
        [JsonProperty("CompletedSteps")]
        [Column("CompletedSteps", SQLiteDbType.Integer, NotNull = true)]
        public int CompletedSteps { get; set; }

        /// <summary>
        /// Gets or sets the error message if the run failed.
        /// </summary>
        [DataMember]
        [JsonProperty("ErrorMessage")]
        [Column("ErrorMessage", SQLiteDbType.Text)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the update name associated with this run.
        /// </summary>
        [DataMember]
        [JsonProperty("UpdateName")]
        [Column("UpdateName", SQLiteDbType.Text)]
        [Index("IX_UpdateRunEntity_UpdateName")]
        public string UpdateName { get; set; }

        /// <summary>
        /// Gets or sets the update version associated with this run.
        /// </summary>
        [DataMember]
        [JsonProperty("UpdateVersion")]
        [Column("UpdateVersion", SQLiteDbType.Text)]
        public string UpdateVersion { get; set; }

        /// <summary>
        /// Gets or sets the action plan ID.
        /// </summary>
        [DataMember]
        [JsonProperty("ActionPlanId")]
        [Column("ActionPlanId", SQLiteDbType.Text)]
        public string ActionPlanId { get; set; }

        /// <summary>
        /// Gets or sets the action plan instance ID.
        /// </summary>
        [DataMember]
        [JsonProperty("ActionPlanInstanceId")]
        [Column("ActionPlanInstanceId", SQLiteDbType.Text)]
        public string ActionPlanInstanceId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateRunEntity"/> class.
        /// </summary>
        public UpdateRunEntity()
        {
            this.State = UpdateRunState.Unknown;
            this.CreatedTime = DateTimeOffset.UtcNow;
            this.LastWriteTime = DateTimeOffset.UtcNow;
            this.IsDeleted = false;
            this.OnCompleteActionSuccess = false;
            this.PreparationDownloadPercentage = 0;
            this.IsPreparationRun = false;
            this.Duration = 0;
            this.TotalSteps = 0;
            this.CompletedSteps = 0;
            this.ResourceType = "updateRuns";
        }
    }
}