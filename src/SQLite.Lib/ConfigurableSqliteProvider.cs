using System;
using Microsoft.Extensions.Logging;

namespace SQLite.Lib
{
    public class ConfigurableSqliteProvider<T> : SqliteProvider<T> where T : class, new()
    {
        private readonly SqliteConfiguration _config;
        private readonly string _connectionString;

        public ConfigurableSqliteProvider(string connectionString, SqliteConfiguration config, ILogger<SqliteProvider<T>> logger = null)
            : base(connectionString, logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connectionString = connectionString;
            ApplyConfiguration();
        }

        private void ApplyConfiguration()
        {
            // Apply configuration through ExecuteCommand
            ExecuteCommand($"PRAGMA foreign_keys = {(_config.EnableForeignKeys ? "ON" : "OFF")};");
            ExecuteCommand($"PRAGMA cache_size = {_config.CacheSize};");
            ExecuteCommand($"PRAGMA page_size = {_config.PageSize};");
            ExecuteCommand($"PRAGMA journal_mode = {_config.JournalMode};");
            ExecuteCommand($"PRAGMA synchronous = {_config.SynchronousMode};");
            ExecuteCommand($"PRAGMA busy_timeout = {_config.BusyTimeout};");
        }
    }
}
