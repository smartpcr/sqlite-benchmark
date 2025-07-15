// -----------------------------------------------------------------------
// <copyright file="SqliteConfiguration.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace SQLite.Lib;

public class SqliteConfiguration
{
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
    /// How logs are maintained.
    /// </summary>
    public JournalMode JournalMode { get; set; } = JournalMode.WAL;

    /// <summary>
    /// Manages how often fsync() should be called.
    /// </summary>
    public SynchronousMode SynchronousMode { get; set; } = SynchronousMode.Normal;

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
    /// By default, SQLite does not enforce foreign-key constraints. This choice was made for backwards compatibility.
    /// Unless you explicitly turn them on, SQLite will happily let you insert “orphan” child rows or delete parent rows
    /// without cascading—or even raising an error.
    /// The FK-enforcement flag lives in memory on each database connection. You must run it after opening every new connection.
    /// </summary>
    public bool EnableForeignKeys { get; set; } = true;
}
