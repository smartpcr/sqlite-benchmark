using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using SQLite.Lib;

namespace SQLite.Benchmark
{
    [Config(typeof(ConfigBenchmarkConfig))]
    [MemoryDiagnoser]
    public class SqliteConfigurationBenchmarks
    {
        private const string DbFolder = @"C:\ClusterStorage\Infrastructure_1\Shares\SU1_Infrastructure_1\Updates\ReliableStore";
        private ConfigurableSqliteProvider<BenchmarkEntity> _provider = null!;
        private string _dbPath = null!;
        private List<BenchmarkEntity> _testData = null!;
        private List<long> _existingIds = null!;

        // Configuration parameters
        [Params("1024", "4096", "1048576", "4194304")] // 1KB, 4KB, 1MB, 4MB in bytes
        public string CacheSize { get; set; }

        [Params("1024", "4096")] // 1KB, 4KB
        public string PageSize { get; set; }

        [Params("WAL", "DELETE", "MEMORY")]
        public string JournalMode { get; set; }

        [Params("OFF", "NORMAL", "FULL")]
        public string SynchronousMode { get; set; }

        [Params(true, false)]
        public bool EnableForeignKeys { get; set; }

        [Params("small", "medium", "large")] // small=1KB, medium=1MB, large=5MB
        public string PayloadSize { get; set; }

        [Params(1000)] // Fixed record count for configuration testing
        public int RecordCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (!Directory.Exists(DbFolder))
            {
                Directory.CreateDirectory(DbFolder);
            }

            _dbPath = Path.Combine(SqliteConfigurationBenchmarks.DbFolder, $"config_benchmark_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={_dbPath};Version=3;";

            // Convert cache size from bytes to pages (divide by page size)
            var cacheSizeInPages = (int.Parse(CacheSize) / int.Parse(PageSize)).ToString();

            var config = new SqliteConfiguration
            {
                CacheSize = cacheSizeInPages,
                PageSize = PageSize,
                JournalMode = JournalMode,
                SynchronousMode = SynchronousMode,
                BusyTimeout = "5000", // Fixed at 5 seconds
                EnableForeignKeys = EnableForeignKeys
            };

            _provider = new ConfigurableSqliteProvider<BenchmarkEntity>(connectionString, config);
            _provider.CreateTable();

            // Prepare test data with varying payload sizes
            var payloadData = GeneratePayloadData();

            _testData = Enumerable.Range(1, RecordCount)
                .Select(i => new BenchmarkEntity
                {
                    Name = $"Entity {i}",
                    Value = i,
                    Description = payloadData, // Use payload data based on size parameter
                    IsActive = i % 2 == 0,
                    Score = i * 1.5,
                    Tags = string.Join(",", Enumerable.Range(1, 5).Select(t => $"tag{t}"))
                })
                .ToList();

            // Insert initial data for read benchmarks
            _provider.InsertBatch(_testData.Take(RecordCount / 2));
            _existingIds = _provider.GetAll().Select(e => e.Id).ToList();
        }

        private string GeneratePayloadData()
        {
            int payloadSizeInBytes = PayloadSize switch
            {
                "small" => 1024,        // 1KB
                "medium" => 1048576,    // 1MB
                "large" => 5242880,     // 5MB
                _ => 1024
            };

            // Generate a string of the specified size
            // Using a repeating pattern to ensure consistent size
            var pattern = "The quick brown fox jumps over the lazy dog. ";
            var patternLength = pattern.Length;
            var repeatCount = payloadSizeInBytes / patternLength;
            var remainder = payloadSizeInBytes % patternLength;

            return string.Concat(Enumerable.Repeat(pattern, repeatCount)) +
                   (remainder > 0 ? pattern.Substring(0, remainder) : "");
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _provider = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); }
                catch { }
            }
        }

        [Benchmark]
        public int SequentialInserts()
        {
            var count = 0;
            for (int i = 0; i < 100; i++)
            {
                _provider.Insert(_testData[i % _testData.Count]);
                count++;
            }
            return count;
        }

        [Benchmark]
        public int BatchInsertWithTransaction()
        {
            using (_provider.BeginTransaction())
            {
                var batch = _testData.Take(100).ToList();
                return _provider.InsertBatch(batch);
            }
        }

        [Benchmark]
        public List<BenchmarkEntity> SequentialReads()
        {
            var results = new List<BenchmarkEntity>();
            foreach (var id in _existingIds.Take(100))
            {
                results.Add(_provider.GetById(id));
            }
            return results;
        }

        [Benchmark]
        public List<BenchmarkEntity> BulkRead()
        {
            return _provider.GetAll().ToList();
        }

        [Benchmark]
        public List<BenchmarkEntity> FilteredRead()
        {
            return _provider.Find(e => e.IsActive && e.Value > RecordCount / 4).ToList();
        }

        [Benchmark]
        public int MixedOperations()
        {
            var operations = 0;

            // Insert
            _provider.Insert(_testData[operations % _testData.Count]);
            operations++;

            // Read
            var entity = _provider.GetById(_existingIds[operations % _existingIds.Count]);
            operations++;

            // Update
            if (entity != null)
            {
                entity.Name = "Updated " + entity.Name;
                _provider.Update(entity);
                operations++;
            }

            // Delete
            if (_existingIds.Count > 10)
            {
                _provider.Delete(_existingIds[operations % 10]);
                operations++;
            }

            return operations;
        }

        [Benchmark]
        public async Task ConcurrentOperations()
        {
            var tasks = new Task[4];

            // Mix of read and write operations
            tasks[0] = Task.Run(() =>
            {
                for (int i = 0; i < 25; i++)
                    _provider.GetAll().Count();
            });

            tasks[1] = Task.Run(() =>
            {
                for (int i = 0; i < 25; i++)
                    _provider.Insert(new BenchmarkEntity { Name = $"Concurrent {i}", Value = i });
            });

            tasks[2] = Task.Run(() =>
            {
                for (int i = 0; i < 25; i++)
                {
                    var id = _existingIds[i % _existingIds.Count];
                    _provider.GetById(id);
                }
            });

            tasks[3] = Task.Run(() =>
            {
                using (_provider.BeginTransaction())
                {
                    for (int i = 0; i < 10; i++)
                        _provider.Insert(new BenchmarkEntity { Name = $"Transaction {i}", Value = i });
                }
            });

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public void ComplexJsonQuery()
        {
            var sql = @"
                SELECT Id, Data
                FROM BenchmarkEntity
                WHERE json_extract(Data, '$.Value') > @minValue
                  AND json_extract(Data, '$.IsActive') = 1
                ORDER BY json_extract(Data, '$.Value') DESC
                LIMIT 50";

            _provider.ExecuteQuery(sql, new { minValue = RecordCount / 2 }).ToList();
        }
    }

    public class ConfigBenchmarkConfig : ManualConfig
    {
        public ConfigBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(2)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));

            AddDiagnoser(MemoryDiagnoser.Default);
            AddColumnProvider(DefaultColumnProviders.Instance);
            AddLogger(ConsoleLogger.Default);

            // Add custom columns to show configuration
            AddColumn(new TagColumn("Cache", name => name.CacheSize));
            AddColumn(new TagColumn("Page", name => name.PageSize));
            AddColumn(new TagColumn("Journal", name => name.JournalMode));
            AddColumn(new TagColumn("Sync", name => name.SynchronousMode));
            AddColumn(new TagColumn("PayloadSize", name => name.PayloadSize));
            AddColumn(new TagColumn("ForeignKeys", name => name.EnableForeignKeys.ToString()));

            WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
        }
    }

    public class TagColumn : IColumn
    {
        private readonly string _columnName;
        private readonly Func<SqliteConfigurationBenchmarks, string> _valueProvider;

        public TagColumn(string columnName, Func<SqliteConfigurationBenchmarks, string> valueProvider)
        {
            _columnName = columnName;
            _valueProvider = valueProvider;
        }

        public string Id => _columnName;
        public string ColumnName => _columnName;
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Params;
        public int PriorityInCategory => 0;
        public bool IsNumeric => false;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => $"{_columnName} configuration value";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            var instance = Activator.CreateInstance(benchmarkCase.Descriptor.Type) as SqliteConfigurationBenchmarks;
            if (instance != null)
            {
                // Set the parameter value from the benchmark case
                var paramName = _columnName switch
                {
                    "Cache" => "CacheSize",
                    "Page" => "PageSize",
                    "Journal" => "JournalMode",
                    "Sync" => "SynchronousMode",
                    "PayloadSize" => "PayloadSize",
                    "ForeignKeys" => "EnableForeignKeys",
                    _ => _columnName
                };

                var param = benchmarkCase.Parameters.Items.FirstOrDefault(p => p.Name == paramName);
                if (param != null)
                {
                    return param.Value?.ToString() ?? "";
                }
            }
            return "";
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public bool IsAvailable(Summary summary) => true;
    }
}
