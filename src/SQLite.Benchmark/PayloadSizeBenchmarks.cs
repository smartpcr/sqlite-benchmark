// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SQLite.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Loggers;
    using BenchmarkDotNet.Reports;
    using SQLite.Lib;
    [Config(typeof(PayloadBenchmarkConfig))]
    [MemoryDiagnoser]
    public class PayloadSizeBenchmarks
    {
        private PersistenceProvider<PayloadEntity> provider = null!;
        private string dbPath = null!;
        private Dictionary<PayloadSize, List<PayloadEntity>> testData = null!;
        private Dictionary<PayloadSize, List<long>> existingIds = null!;

        public enum PayloadSize
        {
            ExtraSmall = 150,      // 150 bytes
            Small = 1_024,         // 1 KB
            Medium = 102_400,      // 100 KB
            Large = 5_242_880,     // 5 MB
            ExtraLarge = 52_428_800 // 50 MB
        }

        [Params(PayloadSize.ExtraSmall, PayloadSize.Small, PayloadSize.Medium, PayloadSize.Large, PayloadSize.ExtraLarge)]
        public PayloadSize Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.dbPath = Path.Combine(Path.GetTempPath(), $"payload_benchmark_{Guid.NewGuid()}.db");

            // Optimize connection string for large payloads
            var connectionString = $"Data Source={this.dbPath};Version=3;Page Size=4096;Cache Size=10000;Journal Mode=WAL;";

            this.provider = new PersistenceProvider<PayloadEntity>(connectionString);
            this.provider.CreateTable();

            // Set SQLite limits for large payloads
            using (var conn = new System.Data.SQLite.SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // Increase maximum SQL statement length for large payloads
                    cmd.CommandText = "PRAGMA max_page_count = 2147483646;"; // ~8TB with 4KB pages
                    cmd.ExecuteNonQuery();

                    // Optimize for large blobs
                    cmd.CommandText = "PRAGMA temp_store = MEMORY;";
                    cmd.ExecuteNonQuery();
                }
            }

            // Prepare test data for all sizes
            this.testData = new Dictionary<PayloadSize, List<PayloadEntity>>();
            this.existingIds = new Dictionary<PayloadSize, List<long>>();

            foreach (PayloadSize size in Enum.GetValues(typeof(PayloadSize)))
            {
                var entities = this.GenerateEntities(size, 10);
                this.testData[size] = entities;

                // Pre-insert some data for read/update/delete benchmarks
                var preInserted = this.GenerateEntities(size, 5);
                foreach (var entity in preInserted)
                {
                    this.provider.Insert(entity);
                }
                this.existingIds[size] = this.provider.GetAll()
                    .Where(e => e.Payload.Length >= (int)size * 0.9) // Account for slight variations
                    .Select(e => e.Id)
                    .ToList();
            }
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

            // Also clean up WAL and SHM files
            var walPath = this.dbPath + "-wal";
            var shmPath = this.dbPath + "-shm";
            if (File.Exists(walPath)) try { File.Delete(walPath); } catch { }
            if (File.Exists(shmPath)) try { File.Delete(shmPath); } catch { }
        }

        private List<PayloadEntity> this.GenerateEntities(PayloadSize size, int count)
        {
            var entities = new List<PayloadEntity>();
            var targetSize = (int)size;

            for (int i = 0; i < count; i++)
            {
                var entity = new PayloadEntity
                {
                    Name = $"Payload_{size}_{i}",
                    PayloadSize = targetSize,
                    Payload = this.GeneratePayload(targetSize),
                    Metadata = $"Entity {i} with {size} payload",
                    CreatedAt = DateTime.UtcNow
                };
                entities.Add(entity);
            }

            return entities;
        }

        private string this.GeneratePayload(int sizeInBytes)
        {
            // Generate a string that will be approximately sizeInBytes when serialized to JSON
            // Account for JSON overhead (quotes, field names, etc.)
            // Estimated JSON overhead for PayloadEntity is about 150 bytes
            int adjustedSize = Math.Max(1, sizeInBytes - 150);

            // For very large payloads, use a more efficient generation method
            if (adjustedSize > 1_000_000) // 1 MB
            {
                var sb = new StringBuilder(adjustedSize);
                var chunk = new string('X', 1000); // 1KB chunks
                var fullChunks = adjustedSize / 1000;
                var remainder = adjustedSize % 1000;

                for (int i = 0; i < fullChunks; i++)
                {
                    sb.Append(chunk);
                }

                if (remainder > 0)
                {
                    sb.Append(new string('X', remainder));
                }

                return sb.ToString();
            }
            else
            {
                // For smaller payloads, use the simpler approach
                return new string('X', adjustedSize);
            }
        }

        [Benchmark]
        public void Insert()
        {
            var entity = this.testData[this.Size][0];
            this.provider.Insert(new PayloadEntity
            {
                Name = entity.Name,
                PayloadSize = entity.PayloadSize,
                Payload = entity.Payload,
                Metadata = entity.Metadata,
                CreatedAt = DateTime.UtcNow
            });
        }

        [Benchmark]
        public void BatchInsert()
        {
            var batch = this.testData[this.Size].Take(5).Select(e => new PayloadEntity
            {
                Name = e.Name,
                PayloadSize = e.PayloadSize,
                Payload = e.Payload,
                Metadata = e.Metadata,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            this.provider.InsertBatch(batch);
        }

        [Benchmark]
        public PayloadEntity Select()
        {
            if (this.existingIds[this.Size].Any())
            {
                var id = this.existingIds[this.Size][0];
                return this.provider.GetById(id);
            }
            return null;
        }

        [Benchmark]
        public bool Update()
        {
            if (this.existingIds[this.Size].Any())
            {
                var id = this.existingIds[this.Size][0];
                var entity = this.provider.GetById(id);
                if (entity != null)
                {
                    entity.Metadata = $"Updated at {DateTime.UtcNow}";
                    return this.provider.Update(entity);
                }
            }
            return false;
        }

        [Benchmark]
        public bool Delete()
        {
            if (this.existingIds[this.Size].Count > 1)
            {
                var id = this.existingIds[this.Size][1];
                return this.provider.Delete(id);
            }
            return false;
        }

        [Benchmark]
        public void TransactionInsert()
        {
            using (this.provider.BeginTransaction())
            {
                var entities = this.testData[this.Size].Take(3).Select(e => new PayloadEntity
                {
                    Name = e.Name,
                    PayloadSize = e.PayloadSize,
                    Payload = e.Payload,
                    Metadata = e.Metadata,
                    CreatedAt = DateTime.UtcNow
                });

                foreach (var entity in entities)
                {
                    this.provider.Insert(entity);
                }
            }
        }

        [Benchmark]
        public List<PayloadEntity> SelectMultiple()
        {
            return this.provider.Find(e => e.PayloadSize == (int)this.Size).Take(3).ToList();
        }
    }

    public class PayloadEntity
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int PayloadSize { get; set; }

        public string Payload { get; set; } = string.Empty;

        public string Metadata { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    public class PayloadBenchmarkConfig : ManualConfig
    {
        public PayloadBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(2)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)); // Run each benchmark only once per iteration due to large data

            AddDiagnoser(MemoryDiagnoser.Default);

            // Add column providers for better output
            AddColumnProvider(DefaultColumnProviders.Instance);

            // Add loggers
            AddLogger(ConsoleLogger.Default);

            // Set summary style
            WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));

            // Set longer timeout for large payloads
            WithOption(ConfigOptions.DisableOptimizationsValidator, true);
        }
    }
}
