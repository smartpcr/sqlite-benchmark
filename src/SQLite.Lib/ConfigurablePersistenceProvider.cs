// -----------------------------------------------------------------------
// <copyright file="ConfigurablePersistenceProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using Microsoft.Extensions.Logging;
    public class ConfigurablePersistenceProvider<T> : PersistenceProvider<T> where T : class, new()
    {
        private readonly SqliteConfiguration config;
        private readonly string connectionString;

        public ConfigurablePersistenceProvider(string connectionString, SqliteConfiguration config, ILogger<PersistenceProvider<T>> logger = null)
            : base(connectionString, logger)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.connectionString = connectionString;
            this.ApplyConfiguration();
        }

        private void ApplyConfiguration()
        {
            // Apply configuration through ExecuteCommand
            this.ExecuteCommand($"PRAGMA foreign_keys = {(this.config.EnableForeignKeys ? "ON" : "OFF")};");
            this.ExecuteCommand($"PRAGMA cache_size = {this.config.CacheSize};");
            this.ExecuteCommand($"PRAGMA page_size = {this.config.PageSize};");
            this.ExecuteCommand($"PRAGMA journal_mode = {this.config.JournalMode};");
            this.ExecuteCommand($"PRAGMA synchronous = {this.config.SynchronousMode};");
            this.ExecuteCommand($"PRAGMA busy_timeout = {this.config.BusyTimeout};");
        }
    }
}