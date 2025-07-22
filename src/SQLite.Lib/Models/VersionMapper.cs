// -----------------------------------------------------------------------
// <copyright file="VersionMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System.Data;
    using System.Data.Common;
    using System.Data.SQLite;
    using System.Linq;
    using SQLite.Lib.Mappings;

    /// <summary>
    /// Mapper for Version entity showing SQL generation.
    /// </summary>
    public class VersionMapper : BaseEntityMapper<VersionEntity, long>
    {
        /// <summary>
        /// Override to handle auto-increment version ID.
        /// </summary>
        public override void AddParameters(DbCommand command, VersionEntity entity)
        {
            // For INSERT operations, don't add the Version parameter since it's auto-increment
            // Only add Timestamp if it's not computed
            var timestampMapping = this.GetPropertyMappings().First(p => p.Key.Name == nameof(VersionEntity.Timestamp)).Value;
            if (!timestampMapping.IsComputed)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@Timestamp";
                parameter.Value = entity.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffzzz");
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Override for SQLiteCommand to handle auto-increment version ID.
        /// </summary>
        public new void AddParameters(SQLiteCommand command, VersionEntity entity)
        {
            this.AddParameters((DbCommand)command, entity);
        }

        /// <summary>
        /// Gets the insert columns excluding auto-increment ID.
        /// </summary>
        public override System.Collections.Generic.List<string> GetInsertColumns()
        {
            var columns = base.GetInsertColumns();
            // Remove Version column since it's auto-increment
            columns.Remove("Version");
            return columns;
        }

        /// <summary>
        /// Creates a command to get the next version number.
        /// </summary>
        public SQLiteCommand CreateGetNextVersionCommand()
        {
            var command = new SQLiteCommand();
            command.CommandText = @$"
                INSERT INTO {this.GetTableName()} (Timestamp) VALUES (datetime('now'));
                SELECT last_insert_rowid();";
            return command;
        }

        /// <summary>
        /// Creates a command to get the current version number.
        /// </summary>
        public SQLiteCommand CreateGetCurrentVersionCommand()
        {
            var command = new SQLiteCommand();
            command.CommandText = $"SELECT MAX(Version) FROM {this.GetTableName()};";
            return command;
        }
    }
}