using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Extensions.Logging;
using SQLite.Lib.Implementations;

namespace SQLite.Tests
{
    [TestClass]
    public class SqliteProviderTests
    {
        private string _dbPath;
        private SqliteProvider<TestEntity> _provider;
        private ILogger<SqliteProvider<TestEntity>> _logger;

        [TestInitialize]
        public void Setup()
        {
            // Create unique database for each test
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={_dbPath};Version=3;";

            // Setup Serilog
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            var loggerFactory = new SerilogLoggerFactory(serilogLogger);
            _logger = loggerFactory.CreateLogger<SqliteProvider<TestEntity>>();

            _provider = new SqliteProvider<TestEntity>(connectionString, _logger);
            _provider.CreateTable();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _provider = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }

        [TestMethod]
        public void CreateTable_ShouldCreateTableSuccessfully()
        {
            // Act - Table already created in Setup
            var count = _provider.Count();

            // Assert
            Assert.AreEqual(0, count, "Newly created table should be empty");
        }

        [TestMethod]
        public void Insert_ShouldInsertEntityAndReturnWithId()
        {
            // Arrange
            var entity = new TestEntity
            {
                Name = "Test Entity",
                Value = 42,
                IsActive = true
            };

            // Act
            var result = _provider.Insert(entity);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Id > 0, "ID should be assigned");
            Assert.AreEqual(entity.Name, result.Name);
            Assert.AreEqual(entity.Value, result.Value);
            Assert.AreEqual(entity.IsActive, result.IsActive);
        }

        [TestMethod]
        public void InsertBatch_ShouldInsertMultipleEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity 1", Value = 1, IsActive = true },
                new TestEntity { Name = "Entity 2", Value = 2, IsActive = false },
                new TestEntity { Name = "Entity 3", Value = 3, IsActive = true }
            };

            // Act
            var count = _provider.InsertBatch(entities);

            // Assert
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, _provider.Count());
        }

        [TestMethod]
        public void GetById_ShouldReturnCorrectEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test", Value = 100 };
            var inserted = _provider.Insert(entity);

            // Act
            var retrieved = _provider.GetById(inserted.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(inserted.Id, retrieved.Id);
            Assert.AreEqual(inserted.Name, retrieved.Name);
            Assert.AreEqual(inserted.Value, retrieved.Value);
        }

        [TestMethod]
        public void GetById_ShouldReturnNullForNonExistentId()
        {
            // Act
            var result = _provider.GetById(999);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Update_ShouldUpdateExistingEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "Original", Value = 1 };
            var inserted = _provider.Insert(entity);

            // Act
            inserted.Name = "Updated";
            inserted.Value = 2;
            var success = _provider.Update(inserted);

            // Assert
            Assert.IsTrue(success);
            var updated = _provider.GetById(inserted.Id);
            Assert.AreEqual("Updated", updated.Name);
            Assert.AreEqual(2, updated.Value);
        }

        [TestMethod]
        public void Delete_ShouldRemoveEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "To Delete", Value = 1 };
            var inserted = _provider.Insert(entity);

            // Act
            var success = _provider.Delete(inserted.Id);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNull(_provider.GetById(inserted.Id));
            Assert.AreEqual(0, _provider.Count());
        }

        [TestMethod]
        public void GetAll_ShouldReturnAllEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity 1", Value = 1 },
                new TestEntity { Name = "Entity 2", Value = 2 },
                new TestEntity { Name = "Entity 3", Value = 3 }
            };
            _provider.InsertBatch(entities);

            // Act
            var all = _provider.GetAll().ToList();

            // Assert
            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.All(e => e.Id > 0));
        }

        [TestMethod]
        public void Find_ShouldReturnFilteredEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Active 1", Value = 10, IsActive = true },
                new TestEntity { Name = "Inactive", Value = 20, IsActive = false },
                new TestEntity { Name = "Active 2", Value = 30, IsActive = true }
            };
            _provider.InsertBatch(entities);

            // Act
            var activeEntities = _provider.Find(e => e.IsActive).ToList();

            // Assert
            Assert.AreEqual(2, activeEntities.Count);
            Assert.IsTrue(activeEntities.All(e => e.IsActive));
        }

        [TestMethod]
        public void Transaction_ShouldCommitSuccessfully()
        {
            // Arrange & Act
            using (_provider.BeginTransaction())
            {
                _provider.Insert(new TestEntity { Name = "TX 1", Value = 1 });
                _provider.Insert(new TestEntity { Name = "TX 2", Value = 2 });
            }

            // Assert
            Assert.AreEqual(2, _provider.Count());
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void Transaction_ShouldRollbackOnError()
        {
            // Arrange & Act
            try
            {
                using (_provider.BeginTransaction())
                {
                    _provider.Insert(new TestEntity { Name = "TX 1", Value = 1 });
                    throw new Exception("Simulated error");
                }
            }
            catch
            {
                // Assert - Should rollback
                Assert.AreEqual(0, _provider.Count());
                throw;
            }
        }

        [TestMethod]
        public void ConcurrentReads_ShouldWorkCorrectly()
        {
            // Arrange
            var entities = Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"Entity {i}", Value = i })
                .ToList();
            _provider.InsertBatch(entities);

            // Act
            var tasks = new List<Task<int>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => _provider.GetAll().Count()));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.IsTrue(tasks.All(t => t.Result == 100));
        }

        [TestMethod]
        public void Vacuum_ShouldExecuteSuccessfully()
        {
            // Arrange
            _provider.Insert(new TestEntity { Name = "Test", Value = 1 });

            // Act & Assert - Should not throw
            _provider.Vacuum();
        }

        [TestMethod]
        public void Analyze_ShouldExecuteSuccessfully()
        {
            // Arrange
            _provider.Insert(new TestEntity { Name = "Test", Value = 1 });

            // Act & Assert - Should not throw
            _provider.Analyze();
        }

        [TestMethod]
        public void ExecuteQuery_ShouldReturnCustomResults()
        {
            // Arrange
            _provider.InsertBatch(new[]
            {
                new TestEntity { Name = "A", Value = 10 },
                new TestEntity { Name = "B", Value = 20 },
                new TestEntity { Name = "C", Value = 30 }
            });

            // Act
            var sql = $"SELECT Id, Data FROM {nameof(TestEntity)} WHERE json_extract(Data, '$.Value') > @minValue";
            var results = _provider.ExecuteQuery(sql, new { minValue = 15 }).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.Value > 15));
        }

        [TestMethod]
        public void LargeDataSet_ShouldHandleEfficiently()
        {
            // Arrange
            const int recordCount = 1000;
            var entities = Enumerable.Range(1, recordCount)
                .Select(i => new TestEntity
                {
                    Name = $"Entity {i}",
                    Value = i,
                    LargeText = new string('X', 1000) // 1KB of data
                })
                .ToList();

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _provider.InsertBatch(entities);
            sw.Stop();

            // Assert
            Assert.AreEqual(recordCount, _provider.Count());
            Assert.IsTrue(sw.ElapsedMilliseconds < 5000, $"Batch insert took {sw.ElapsedMilliseconds}ms");
        }
    }

    public class TestEntity
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string LargeText { get; set; }
    }
}
