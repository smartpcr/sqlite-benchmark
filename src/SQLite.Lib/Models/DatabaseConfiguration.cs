using System.ComponentModel.DataAnnotations;

namespace SQLite.Lib.Models
{
    public class DatabaseConfiguration
    {
        public string ConnectionString { get; set; } = "Data Source=:memory:";

        /// <summary>
        /// The suggested maximum number of database pages SQLite will hold in RAM per open database connection.
        /// It’s an upper bound on the page cache. Default value is '-2000', which means "enough pages to use ≈ 2000 × 1024 bytes”
        /// of memory (~2MB), regardless of the page size.
        /// N &gt; 0: sets the cache to N pages.
        /// K &lt; 0: ets the cache so that K × 1024 bytes of memory is used
        /// </summary>
        public int CacheSize { get; set; } = -2000; // 2MB cache

        /// <summary>
        /// The unit of I/O in SQLite, default to 4096 bytes per page.
        /// Every database file is a sequence of fixed-size pages; internal B-tree nodes, table rows, index entries—all live inside pages.
        /// </summary>
        [Range(512, 65536)]
        public int PageSize { get; set; } = 4096;

        /// <summary>
        /// By default, SQLite does not enforce foreign-key constraints. This choice was made for backwards compatibility.
        /// Unless you explicitly turn them on, SQLite will happily let you insert “orphan” child rows or delete parent rows
        /// without cascading—or even raising an error.
        /// The FK-enforcement flag lives in memory on each database connection. You must run it after opening every new connection.
        /// </summary>
        public bool EnableForeignKeys { get; set; } = true;

        /// <summary>
        /// When a table is locked by another connection, SQLite will sleep and retry for up to the specified number of milliseconds
        /// before returning SQLITE_BUSY.
        /// By default, the busy timeout is 0 ms—meaning “don’t wait at all.” If you need to smooth over transient locks
        /// under normal concurrency, you should explicitly set a nonzero timeout each time you open a connection.
        /// The setting applies per database connection and only lasts for that session. If you close and reopen the connection,
        /// you must issue PRAGMA busy_timeout again
        /// </summary>
        public int BusyTimeout { get; set; } = 5000; // 5 seconds

        /// <summary>
        /// How logs are maintained.
        /// </summary>
        public JournalMode JournalMode { get; set; } = JournalMode.WAL;

        /// <summary>
        /// Manages how often fsync() should be called.
        /// </summary>
        public SynchronousMode SynchronousMode { get; set; } = SynchronousMode.Normal;
    }

    public enum JournalMode
    {
        /// <summary>
        /// The rollback journal file is created at the start of each transaction and deleted when the transaction commits.
        /// This is the simplest and most portable mode (default).
        /// pros:
        /// 1. Maximum safety: full ability to roll back on crash
        /// 2. Works on all filesystems.
        /// cons:
        /// File‐system overhead deleting the journal on every commit.
        /// </summary>
        Delete,

        /// <summary>
        /// Similar to DELETE, but instead of deleting the journal file, SQLite truncates it to zero length at commit.
        /// On many platforms, truncating is faster than deleting because it avoids directory‐update overhead.
        /// pros: Lower overhead than DELETE on filesystems where truncation beats deletion.
        /// cons: Journal file still occupies a directory entry.
        /// </summary>
        Truncate,

        /// <summary>
        /// The journal file remains on disk, but its header is overwritten with zeros rather than deleted or truncated.
        /// This prevents other connections from rolling back using the old journal, yet avoids file‐creation/deletion costs
        /// pros:
        /// 1. Very low file‐system churn (no create/truncate/delete).
        /// 2. Good for environments where file‐ops are expensive.
        /// cons:
        /// Journal file may grow large over time (e.g. after VACUUM).
        /// </summary>
        Persist,

        /// <summary>
        /// The rollback journal is kept entirely in RAM, never on disk. This eliminates disk I/O for journaling—but
        /// if your process crashes mid-transaction, the database can become corrupt since there’s no on-disk journal
        /// to recover from.
        /// pros:
        /// 1. Fastest possible commits (no disk writes).
        /// 2. Ideal for ephemeral or in-memory databases.
        /// cons:
        /// 1. No crash recovery.
        /// 2. Not suitable for persistent data.
        /// </summary>
        Memory,

        /// <summary>
        /// Write-Ahead Logging mode (“WAL”) is an alternative journaling scheme that can dramatically improve concurrency and,
        /// in many cases, write performance. Under the hood, WAL shifts from the default “rollback-journal” (DELETE) mode—
        /// where updates are journaled by making a copy of the original page before modifying—to an append-only log of changes
        /// that is kept in a separate “-wal” file.
        /// pros:
        /// 1. Concurrent readers never block a writer, and a writer doesn’t block readers, giving much higher concurrency.
        /// 2. Fewer fsync() calls—better performance on flaky filesystems.
        /// cons:
        /// 1. You must distribute three files (.db, -wal, -shm).
        /// 2. WAL files can grow large without regular checkpoints.
        /// 3. Not supported on some network‐mounted filesystems.
        /// </summary>
        WAL,

        /// <summary>
        /// Disables the rollback journal entirely. No journal file is ever created, and ROLLBACK no longer works
        /// (its behavior is undefined).
        /// If a crash occurs mid-transaction, the database is very likely to corrupt.
        /// pros: Zero journaling overhead.
        /// cons:
        /// 1. No atomic commit/rollback guarantees.
        /// 2. Database corruption is almost certain on failure.
        /// </summary>
        Off
    }

    public enum SynchronousMode
    {
        /// <summary>
        /// SQLite does not invoke fsync() (or equivalent) on the database or journal file before a commit. Writes go to the OS cache only.
        /// cons: May lose or corrupt the last transactions on crash.
        /// typical use case: Bulk loads, non-critical data
        /// </summary>
        Off = 0,

        /// <summary>
        /// default, SQLite calls fsync() on the journal file at commit, but does not always fsync() the database file header.
        /// In DELETE mode it still truncates/deletes the journal.
        /// pros: Consistent DB file + rollback journal intact; may lose the very last commit if power fails at the wrong moment.
        /// cons: good balance of safety and speed.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// SQLite calls fsync() on both the journal file and the database file before returning from COMMIT.
        /// Almost no risk of corruption, even on power-fail.
        /// Only used when transactional systems requiring maximum safety
        /// </summary>
        Full = 2,

        /// <summary>
        /// In addition to FULL, SQLite also issues extra syncs when writing rollback-journal pages (one fsync() per page write).
        /// Even safer in edge cases (filesystem bugs).
        /// Used when vry high-assurance workloads on flaky storage.
        /// </summary>
        Extra = 3
    }
}
