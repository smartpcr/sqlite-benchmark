namespace SQLite.Lib.Models
{
    public class DatabaseConfiguration
    {
        public string ConnectionString { get; set; } = "Data Source=:memory:";
        public bool EnableWAL { get; set; } = true;
        public int CacheSize { get; set; } = -2000; // 2MB cache
        public int PageSize { get; set; } = 4096;
        public bool EnableForeignKeys { get; set; } = true;
        public int BusyTimeout { get; set; } = 5000; // 5 seconds
        public JournalMode JournalMode { get; set; } = JournalMode.WAL;
        public SynchronousMode SynchronousMode { get; set; } = SynchronousMode.Normal;
    }

    public enum JournalMode
    {
        Delete,
        Truncate,
        Persist,
        Memory,
        WAL,
        Off
    }

    public enum SynchronousMode
    {
        Off = 0,
        Normal = 1,
        Full = 2,
        Extra = 3
    }
}
