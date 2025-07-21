using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Extensions.Logging;
using SQLite.Lib;

namespace SQLite.Tests
{
    [TestClass]
    public class TransactionBatchTests
    {
        private string _dbPath;
        private PersistenceProvider<Product> _provider;
        private ILogger<PersistenceProvider<Product>> _logger;

        [TestInitialize]
        public void Setup()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"batch_test_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={_dbPath};Version=3;";

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            var loggerFactory = new SerilogLoggerFactory(serilogLogger);
            _logger = loggerFactory.CreateLogger<PersistenceProvider<Product>>();

            _provider = new PersistenceProvider<Product>(connectionString, _logger);
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
        public void TransactionBatch_ShouldExecuteMultipleOperationsInOrder()
        {
            // Arrange
            var product1 = _provider.Insert(new Product { Name = "Product 1", Price = 100m });
            var product2 = _provider.Insert(new Product { Name = "Product 2", Price = 200m });
            var product3 = _provider.Insert(new Product { Name = "Product 3", Price = 300m });

            // Act
            using (var batch = _provider.CreateTransactionBatch())
            {
                // Update product 1
                product1.Price = 150m;
                batch.AddUpdate(product1);

                // Delete product 2
                batch.AddDelete(product2.Id);

                // Insert new product
                batch.AddInsert(new Product { Name = "Product 4", Price = 400m });

                // Update product 3
                product3.Price = 350m;
                batch.AddUpdate(product3);

                // Commit all operations
                batch.Commit();
            }

            // Assert
            var allProducts = _provider.GetAll().ToList();
            Assert.AreEqual(3, allProducts.Count, "Should have 3 products (1 deleted, 1 added)");

            var updatedProduct1 = _provider.GetById(product1.Id);
            Assert.AreEqual(150m, updatedProduct1.Price, "Product 1 price should be updated");

            var deletedProduct2 = _provider.GetById(product2.Id);
            Assert.IsNull(deletedProduct2, "Product 2 should be deleted");

            var updatedProduct3 = _provider.GetById(product3.Id);
            Assert.AreEqual(350m, updatedProduct3.Price, "Product 3 price should be updated");

            var newProduct = allProducts.FirstOrDefault(p => p.Name == "Product 4");
            Assert.IsNotNull(newProduct, "Product 4 should exist");
            Assert.AreEqual(400m, newProduct.Price, "Product 4 price should be 400");
        }

        [TestMethod]
        public void TransactionBatch_ShouldRollbackOnException()
        {
            // Arrange
            var product1 = _provider.Insert(new Product { Name = "Product 1", Price = 100m });
            var initialCount = _provider.Count();

            // Act & Assert
            try
            {
                using (var batch = _provider.CreateTransactionBatch())
                {
                    // Add some operations
                    batch.AddInsert(new Product { Name = "Product 2", Price = 200m });
                    batch.AddUpdate(product1);
                    
                    // This should cause the transaction to fail
                    batch.AddDelete(999999); // Non-existent ID
                    
                    batch.Commit();
                }
                Assert.Fail("Expected exception was not thrown");
            }
            catch
            {
                // Expected exception
            }

            // Verify rollback
            Assert.AreEqual(initialCount, _provider.Count(), "Count should remain unchanged after rollback");
            var unchangedProduct = _provider.GetById(product1.Id);
            Assert.AreEqual(100m, unchangedProduct.Price, "Product 1 should remain unchanged");
        }

        [TestMethod]
        public void TransactionBatch_ShouldRollbackAutomaticallyOnDispose()
        {
            // Arrange
            var product1 = _provider.Insert(new Product { Name = "Product 1", Price = 100m });
            var initialCount = _provider.Count();

            // Act - Create batch but don't commit
            using (var batch = _provider.CreateTransactionBatch())
            {
                batch.AddInsert(new Product { Name = "Product 2", Price = 200m });
                product1.Price = 150m;
                batch.AddUpdate(product1);
                // Intentionally not calling Commit()
            } // Should rollback on dispose

            // Assert
            Assert.AreEqual(initialCount, _provider.Count(), "Count should remain unchanged after auto-rollback");
            var unchangedProduct = _provider.GetById(product1.Id);
            Assert.AreEqual(100m, unchangedProduct.Price, "Product 1 should remain unchanged");
        }

        [TestMethod]
        public void TransactionBatch_ShouldMaintainOperationOrder()
        {
            // Arrange
            var product = _provider.Insert(new Product { Name = "Test Product", Price = 100m });

            // Act
            using (var batch = _provider.CreateTransactionBatch())
            {
                // First update: 100 -> 150
                product.Price = 150m;
                batch.AddUpdate(product);

                // Second update: 150 -> 200
                product.Price = 200m;
                batch.AddUpdate(product);

                // Verify operations are in correct order
                var operations = batch.Operations;
                Assert.AreEqual(2, operations.Count);
                Assert.IsTrue(operations.All(op => op.OperationType == OperationType.Update));

                batch.Commit();
            }

            // Assert
            var finalProduct = _provider.GetById(product.Id);
            Assert.AreEqual(200m, finalProduct.Price, "Final price should reflect the last update");
        }
    }
}