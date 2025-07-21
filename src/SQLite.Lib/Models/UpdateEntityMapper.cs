// -----------------------------------------------------------------------
// <copyright file="UpdateEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using Newtonsoft.Json;

    /// <summary>
    /// Example mapper for UpdateEntity showing SQL generation.
    /// </summary>
    public class UpdateEntityMapper : ISQLiteEntityMapper<UpdateEntity, string>
    {
        public string GetTableName() => "Updates";

        public string GetPrimaryKeyColumn() => "UpdateId";

        public List<string> GetSelectColumns() => new List<string>
        {
            "UpdateId", "UpdateName", "Version", "Description", "Publisher",
            "ReleaseDate", "PackageType", "PackageSize", "DownloadUrl",
            "Dependencies", "Prerequisites", "State", "Priority",
            "CreatedTime", "LastWriteTime", "Version AS EntityVersion", "IsDeleted", "ExpirationTime"
        };

        public List<string> GetInsertColumns() => new List<string>
        {
            "UpdateId", "UpdateName", "Version", "Description", "Publisher",
            "ReleaseDate", "PackageType", "PackageSize", "DownloadUrl",
            "Dependencies", "Prerequisites", "State", "Priority",
            "CreatedTime", "LastWriteTime", "Version", "IsDeleted", "ExpirationTime"
        };

        public List<string> GetUpdateColumns() => new List<string>
        {
            "UpdateName", "Version", "Description", "Publisher",
            "ReleaseDate", "PackageType", "PackageSize", "DownloadUrl",
            "Dependencies", "Prerequisites", "State", "Priority",
            "LastWriteTime", "Version", "ExpirationTime"
        };

        public void AddParameters(SQLiteCommand command, UpdateEntity entity)
        {
            command.Parameters.AddWithValue("@UpdateId", entity.Id);
            command.Parameters.AddWithValue("@UpdateName", entity.UpdateName);
            command.Parameters.AddWithValue("@Version", entity.UpdateVersion);
            command.Parameters.AddWithValue("@Description", entity.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Publisher", entity.Publisher);
            command.Parameters.AddWithValue("@ReleaseDate", entity.ReleaseDate.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("@PackageType", entity.PackageType);
            command.Parameters.AddWithValue("@PackageSize", entity.PackageSize);
            command.Parameters.AddWithValue("@DownloadUrl", entity.DownloadUrl ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Dependencies", JsonConvert.SerializeObject(entity.Dependencies));
            command.Parameters.AddWithValue("@Prerequisites", JsonConvert.SerializeObject(entity.Prerequisites));
            command.Parameters.AddWithValue("@State", entity.State.ToString());
            command.Parameters.AddWithValue("@Priority", (int)entity.Priority);
            command.Parameters.AddWithValue("@CreatedTime", entity.CreatedTime.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("@LastWriteTime", entity.LastWriteTime.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("@Version", entity.Version);
            command.Parameters.AddWithValue("@IsDeleted", entity.IsDeleted ? 1 : 0);
            command.Parameters.AddWithValue("@ExpirationTime",
                entity.ExpirationTime?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
        }

        public UpdateEntity MapFromReader(IDataReader reader)
        {
            return new UpdateEntity
            {
                Id = reader.GetString(reader.GetOrdinal("UpdateId")),
                UpdateName = reader.GetString(reader.GetOrdinal("UpdateName")),
                UpdateVersion = reader.GetString(reader.GetOrdinal("Version")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null : reader.GetString(reader.GetOrdinal("Description")),
                Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                ReleaseDate = DateTimeOffset.FromUnixTimeSeconds(
                    reader.GetInt64(reader.GetOrdinal("ReleaseDate"))),
                PackageType = reader.GetString(reader.GetOrdinal("PackageType")),
                PackageSize = reader.GetInt64(reader.GetOrdinal("PackageSize")),
                DownloadUrl = reader.IsDBNull(reader.GetOrdinal("DownloadUrl"))
                    ? null : reader.GetString(reader.GetOrdinal("DownloadUrl")),
                Dependencies = JsonConvert.DeserializeObject<List<string>>(
                    reader.GetString(reader.GetOrdinal("Dependencies"))),
                Prerequisites = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    reader.GetString(reader.GetOrdinal("Prerequisites"))),
                State = (UpdateState)Enum.Parse(typeof(UpdateState), reader.GetString(reader.GetOrdinal("State"))),
                Priority = (UpdatePriority)reader.GetInt32(reader.GetOrdinal("Priority")),
                CreatedTime = DateTimeOffset.FromUnixTimeSeconds(
                    reader.GetInt64(reader.GetOrdinal("CreatedTime"))),
                LastWriteTime = DateTimeOffset.FromUnixTimeSeconds(
                    reader.GetInt64(reader.GetOrdinal("LastWriteTime"))),
                Version = reader.GetInt64(reader.GetOrdinal("EntityVersion")),
                IsDeleted = reader.GetInt32(reader.GetOrdinal("IsDeleted")) == 1,
                ExpirationTime = reader.IsDBNull(reader.GetOrdinal("ExpirationTime"))
                    ? null : DateTimeOffset.FromUnixTimeSeconds(
                        reader.GetInt64(reader.GetOrdinal("ExpirationTime")))
            };
        }

        public string SerializeKey(string key) => key;

        public string DeserializeKey(string serialized) => serialized;
    }
}