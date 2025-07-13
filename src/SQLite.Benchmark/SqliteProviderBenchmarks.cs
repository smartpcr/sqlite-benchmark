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
using SQLite.Lib;

namespace SQLite.Benchmark
{
    [Config(typeof(BenchmarkConfig))]
    [MemoryDiagnoser]
    public class SqliteProviderBenchmarks
    {
        private SqliteProvider<BenchmarkEntity> _provider = null!;
        private string _dbPath = null!;
        private List<BenchmarkEntity> _testData = null!;
        private List<long> _existingIds = null!;

        [Params(100, 1000, 10000)] public int RecordCount { get; set; }

        [Params(1, 4, 8)] public int ThreadCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={_dbPath};Version=3;";

            _provider = new SqliteProvider<BenchmarkEntity>(connectionString);
            _provider.CreateTable();

            // Prepare test data
            _testData = Enumerable.Range(1, RecordCount)
                .Select(i => new BenchmarkEntity
                {
                    Name = $"Entity {i}",
                    Value = i,
                    Description = $"Description for entity {i} with some additional text to make it more realistic",
                    IsActive = i % 2 == 0,
                    Score = i * 1.5,
                    Tags = string.Join(",", Enumerable.Range(1, 5).Select(t => $"tag{t}"))
                })
                .ToList();

            // Insert some initial data for read/update/delete benchmarks
            _provider.InsertBatch(_testData.Take(RecordCount / 2));
            _existingIds = _provider.GetAll().Select(e => e.Id).ToList();
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
        public void SingleInsert()
        {
            var entity = _testData[0];
            _provider.Insert(entity);
        }

        [Benchmark]
        public int BatchInsert()
        {
            var batch = _testData.Take(100).ToList();
            return _provider.InsertBatch(batch);
        }

        [Benchmark]
        public BenchmarkEntity SingleSelect()
        {
            var id = _existingIds[_existingIds.Count / 2];
            return _provider.GetById(id);
        }

        [Benchmark]
        public List<BenchmarkEntity> SelectAll()
        {
            return _provider.GetAll().ToList();
        }

        [Benchmark]
        public List<BenchmarkEntity> SelectWithFilter()
        {
            return _provider.Find(e => e.IsActive && e.Value > RecordCount / 4).ToList();
        }

        [Benchmark]
        public bool SingleUpdate()
        {
            var entity = _provider.GetById(_existingIds[0]);
            if (entity != null)
            {
                entity.Name = "Updated " + entity.Name;
                entity.Value++;
                return _provider.Update(entity);
            }

            return false;
        }

        [Benchmark]
        public bool SingleDelete()
        {
            if (_existingIds.Any())
            {
                var id = _existingIds[0];
                _existingIds.RemoveAt(0);
                return _provider.Delete(id);
            }

            return false;
        }

        [Benchmark]
        public long CountAll()
        {
            return _provider.Count();
        }

        [Benchmark]
        public void TransactionBatchInsert()
        {
            using (_provider.BeginTransaction())
            {
                for (int i = 0; i < 10; i++)
                {
                    _provider.Insert(_testData[i]);
                }
            }
        }

        [Benchmark]
        public async Task ConcurrentReads()
        {
            var tasks = new Task[ThreadCount];
            for (int i = 0; i < ThreadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        _provider.GetAll().Count();
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task ConcurrentWrites()
        {
            var tasks = new Task[ThreadCount];
            for (int i = 0; i < ThreadCount; i++)
            {
                var threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 5; j++)
                    {
                        var entity = new BenchmarkEntity { Name = $"Concurrent {threadId}-{j}", Value = threadId * 100 + j };
                        _provider.Insert(entity);
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public void ComplexQuery()
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

    public class BenchmarkEntity
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public double Score { get; set; }
        public string Tags { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10));

            AddDiagnoser(MemoryDiagnoser.Default);
            
            // Add column providers for better output
            AddColumnProvider(DefaultColumnProviders.Instance);
            
            // Add loggers
            AddLogger(ConsoleLogger.Default);
            
            // Set summary style
            WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
        }
    }
}
