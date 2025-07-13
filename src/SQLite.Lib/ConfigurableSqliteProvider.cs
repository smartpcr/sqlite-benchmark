using System;
using Microsoft.Extensions.Logging;

namespace SQLite.Lib
{
    public class SqliteConfiguration
    {
        public string CacheSize { get; set; } = "2000"; // Default 2000 pages
        public string PageSize { get; set; } = "4096"; // Default 4096 bytes
        public string JournalMode { get; set; } = "WAL"; // WAL, DELETE, TRUNCATE, PERSIST, MEMORY, OFF
        public string SynchronousMode { get; set; } = "NORMAL"; // OFF, NORMAL, FULL, EXTRA
        public string BusyTimeout { get; set; } = "5000"; // Default 5000ms (5 seconds)
        public bool EnableForeignKeys { get; set; } = true; // Default enabled
    }

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