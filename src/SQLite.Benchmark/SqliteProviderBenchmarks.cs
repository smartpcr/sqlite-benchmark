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
    using SQLite.Lib;
    [Config(typeof(BenchmarkConfig))]
    [MemoryDiagnoser]
    public class SqliteProviderBenchmarks
    {
        private PersistenceProvider<BenchmarkEntity> provider = null!;
        private string dbPath = null!;
        private List<BenchmarkEntity> testData = null!;
        private List<long> existingIds = null!;

        [Params(100, 1000, 10000)] public int RecordCount { get; set; }

        [Params(1, 4, 8)] public int ThreadCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.dbPath = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={this.dbPath};Version=3;";

            this.provider = new PersistenceProvider<BenchmarkEntity>(connectionString);
            this.provider.CreateTable();

            // Prepare test data
            this.testData = Enumerable.Range(1, this.RecordCount)
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
            this.provider.InsertBatch(this.testData.Take(this.RecordCount / 2));
            this.existingIds = this.provider.GetAll().Select(e => e.Id).ToList();
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
        public void SingleInsert()
        {
            var entity = this.testData[0];
            this.provider.Insert(entity);
        }

        [Benchmark]
        public int BatchInsert()
        {
            var batch = this.testData.Take(100).ToList();
            return this.provider.InsertBatch(batch);
        }

        [Benchmark]
        public BenchmarkEntity SingleSelect()
        {
            var id = this.existingIds[this.existingIds.Count / 2];
            return this.provider.GetById(id);
        }

        [Benchmark]
        public List<BenchmarkEntity> SelectAll()
        {
            return this.provider.GetAll().ToList();
        }

        [Benchmark]
        public List<BenchmarkEntity> SelectWithFilter()
        {
            return this.provider.Find(e => e.IsActive && e.Value > this.RecordCount / 4).ToList();
        }

        [Benchmark]
        public bool SingleUpdate()
        {
            var entity = this.provider.GetById(this.existingIds[0]);
            if (entity != null)
            {
                entity.Name = "Updated " + entity.Name;
                entity.Value++;
                return this.provider.Update(entity);
            }

            return false;
        }

        [Benchmark]
        public bool SingleDelete()
        {
            if (this.existingIds.Any())
            {
                var id = this.existingIds[0];
                this.existingIds.RemoveAt(0);
                return this.provider.Delete(id);
            }

            return false;
        }

        [Benchmark]
        public long CountAll()
        {
            return this.provider.Count();
        }

        [Benchmark]
        public void TransactionBatchInsert()
        {
            using (this.provider.BeginTransaction())
            {
                for (int i = 0; i < 10; i++)
                {
                    this.provider.Insert(this.testData[i]);
                }
            }
        }

        [Benchmark]
        public async Task ConcurrentReads()
        {
            var tasks = new Task[ThreadCount];
            for (int i = 0; i < this.ThreadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        this.provider.GetAll().Count();
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task ConcurrentWrites()
        {
            var tasks = new Task[ThreadCount];
            for (int i = 0; i < this.ThreadCount; i++)
            {
                var threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 5; j++)
                    {
                        var entity = new BenchmarkEntity { Name = $"Concurrent {threadId}-{j}", Value = threadId * 100 + j };
                        this.provider.Insert(entity);
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

            this.provider.ExecuteQuery(sql, new { minValue = this.RecordCount / 2 }).ToList();
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
