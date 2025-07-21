// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SQLite.Benchmark
{
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
    [Config(typeof(ConfigBenchmarkConfig))]
    [MemoryDiagnoser]
    public class SqliteConfigurationBenchmarks
    {
        private const string DbFolder = @"C:\ClusterStorage\Infrastructure_1\Shares\SU1_Infrastructure_1\Updates\ReliableStore";
        private ConfigurablePersistenceProvider<BenchmarkEntity> provider = null!;
        private string dbPath = null!;
        private List<BenchmarkEntity> testData = null!;
        private List<long> existingIds = null!;

        // Configuration parameters
        [Params(-500, -2000, -5000)] // 500KB, 2MB, 5MB in bytes
        public int CacheSize { get; set; }

        [Params(1024, 4096)] // 1KB, 4KB
        public int PageSize { get; set; }

        [Params(JournalMode.WAL, JournalMode.Delete, JournalMode.Memory)]
        public JournalMode JournalMode { get; set; }

        [Params(SynchronousMode.Off, SynchronousMode.Normal, SynchronousMode.Full)]
        public SynchronousMode SynchronousMode { get; set; }

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

            this.dbPath = Path.Combine(DbFolder, $"config_benchmark_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={this.dbPath};Version=3;";

            var config = new SqliteConfiguration
            {
                CacheSize = this.CacheSize,
                PageSize = this.PageSize,
                JournalMode = this.JournalMode,
                SynchronousMode = this.SynchronousMode,
                BusyTimeout = 5000, // Fixed at 5 seconds
                EnableForeignKeys = this.EnableForeignKeys
            };

            this.provider = new ConfigurablePersistenceProvider<BenchmarkEntity>(connectionString, config);
            this.provider.CreateTable();

            // Prepare test data with varying payload sizes
            var payloadData = this.GeneratePayloadData();

            this.testData = Enumerable.Range(1, this.RecordCount)
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
            this.provider.InsertBatch(this.testData.Take(this.RecordCount / 2));
            this.existingIds = this.provider.GetAll().Select(e => e.Id).ToList();
        }

        private string this.GeneratePayloadData()
        {
            int payloadSizeInBytes = this.PayloadSize switch
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
            this.provider = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(this.dbPath))
            {
                try { File.Delete(this.dbPath); }
                catch { }
            }
        }

        [Benchmark]
        public int SequentialInserts()
        {
            var count = 0;
            for (int i = 0; i < 100; i++)
            {
                this.provider.Insert(this.testData[i % this.testData.Count]);
                count++;
            }
            return count;
        }

        [Benchmark]
        public int BatchInsertWithTransaction()
        {
            using (this.provider.BeginTransaction())
            {
                var batch = this.testData.Take(100).ToList();
                return this.provider.InsertBatch(batch);
            }
        }

        [Benchmark]
        public List<BenchmarkEntity> SequentialReads()
        {
            var results = new List<BenchmarkEntity>();
            foreach (var id in this.existingIds.Take(100))
            {
                results.Add(this.provider.GetById(id));
            }
            return results;
        }

        [Benchmark]
        public List<BenchmarkEntity> BulkRead()
        {
            return this.provider.GetAll().ToList();
        }

        [Benchmark]
        public List<BenchmarkEntity> FilteredRead()
        {
            return this.provider.Find(e => e.IsActive && e.Value > this.RecordCount / 4).ToList();
        }

        [Benchmark]
        public int MixedOperations()
        {
            var operations = 0;

            // Insert
            this.provider.Insert(this.testData[operations % this.testData.Count]);
            operations++;

            // Read
            var entity = this.provider.GetById(this.existingIds[operations % this.existingIds.Count]);
            operations++;

            // Update
            if (entity != null)
            {
                entity.Name = "Updated " + entity.Name;
                this.provider.Update(entity);
                operations++;
            }

            // Delete
            if (this.existingIds.Count > 10)
            {
                this.provider.Delete(this.existingIds[operations % 10]);
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
                    this.provider.GetAll().Count();
            });

            tasks[1] = Task.Run(() =>
            {
                for (int i = 0; i < 25; i++)
                    this.provider.Insert(new BenchmarkEntity { Name = $"Concurrent {i}", Value = i });
            });

            tasks[2] = Task.Run(() =>
            {
                for (int i = 0; i < 25; i++)
                {
                    var id = this.existingIds[i % this.existingIds.Count];
                    this.provider.GetById(id);
                }
            });

            tasks[3] = Task.Run(() =>
            {
                using (this.provider.BeginTransaction())
                {
                    for (int i = 0; i < 10; i++)
                        this.provider.Insert(new BenchmarkEntity { Name = $"Transaction {i}", Value = i });
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

            this.provider.ExecuteQuery(sql, new { minValue = this.RecordCount / 2 }).ToList();
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
            WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
        }
    }

    public class TagColumn : IColumn
    {
        private readonly string columnName;
        private readonly Func<SqliteConfigurationBenchmarks, string> valueProvider;

        public TagColumn(string columnName, Func<SqliteConfigurationBenchmarks, string> valueProvider)
        {
            this.columnName = columnName;
            this.valueProvider = valueProvider;
        }

        public string Id => this.columnName;

        public string ColumnName => this.columnName;

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Params;

        public int PriorityInCategory => 0;

        public bool IsNumeric => false;

        public UnitType UnitType => UnitType.Dimensionless;

        public string Legend => $"{this.columnName} configuration value";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            var instance = Activator.CreateInstance(benchmarkCase.Descriptor.Type) as SqliteConfigurationBenchmarks;
            if (instance != null)
            {
                // Set the parameter value from the benchmark case
                var paramName = this.columnName switch
                {
                    "Cache" => "CacheSize",
                    "Page" => "PageSize",
                    "Journal" => "JournalMode",
                    "Sync" => "SynchronousMode",
                    "PayloadSize" => "PayloadSize",
                    "ForeignKeys" => "EnableForeignKeys",
                    _ => this.columnName
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
