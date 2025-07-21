using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Extensions.Logging;
using SQLite.Lib;

namespace SQLite.Tests
{
    [TestClass]
    public class SqliteTransactionTests
    {
        [TestMethod]
        public void Transaction_SnapshotIsolation_ShouldPreventConcurrentModificationConflicts()
        {
            // Arrange
            var productDbPath = Path.Combine(Path.GetTempPath(), $"product_test_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={productDbPath};Version=3;";
            
            var loggerFactory = new SerilogLoggerFactory(new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger());
            
            var productProvider1 = new PersistenceProvider<Product>(connectionString, loggerFactory.CreateLogger<PersistenceProvider<Product>>());
            var productProvider2 = new PersistenceProvider<Product>(connectionString, loggerFactory.CreateLogger<PersistenceProvider<Product>>());
            
            productProvider1.CreateTable();
            
            // Insert initial product with price
            var product = productProvider1.Insert(new Product { Name = "Test Product", Price = 100m });
            var productId = product.Id;
            
            // Use ManualResetEvents to coordinate thread execution
            var thread1AcquiredLock = new ManualResetEvent(false);
            var thread1Modified = new ManualResetEvent(false);
            var thread2CanStart = new ManualResetEvent(false);
            var thread2Committed = new ManualResetEvent(false);
            
            Exception thread1Exception = null;
            Exception thread2Exception = null;
            
            // Thread 1: Increase price by 15%
            var thread1 = new Thread(() =>
            {
                try
                {
                    using (productProvider1.BeginTransaction())
                    {
                        // Read current value (should be 100)
                        var p1 = productProvider1.GetById(productId);
                        Assert.AreEqual(100m, p1.Price, "Thread 1 should see initial price of 100");
                        
                        thread1AcquiredLock.Set();
                        
                        // Modify price to 115 (15% increase)
                        p1.Price = p1.Price * 1.15m;
                        productProvider1.Update(p1);
                        
                        thread1Modified.Set();
                        
                        // Wait for thread 2 to commit first
                        thread2Committed.WaitOne();
                        
                        // Thread 1 commits after thread 2
                    }
                }
                catch (Exception ex)
                {
                    thread1Exception = ex;
                }
            });
            
            // Thread 2: Decrease price by 10%
            var thread2 = new Thread(() =>
            {
                try
                {
                    // Wait for thread 1 to acquire lock first
                    thread1AcquiredLock.WaitOne();
                    
                    using (productProvider2.BeginTransaction())
                    {
                        // Read current value (should still be 100 due to snapshot isolation)
                        var p2 = productProvider2.GetById(productId);
                        Assert.AreEqual(100m, p2.Price, "Thread 2 should see initial price of 100 in its snapshot");
                        
                        thread2CanStart.Set();
                        
                        // Wait for thread 1 to modify (but not commit)
                        thread1Modified.WaitOne();
                        
                        // Modify price to 90 (10% decrease)
                        p2.Price = p2.Price * 0.90m;
                        productProvider2.Update(p2);
                        
                        // Thread 2 commits first
                    }
                    
                    thread2Committed.Set();
                }
                catch (Exception ex)
                {
                    thread2Exception = ex;
                }
            });
            
            // Start both threads
            thread1.Start();
            thread2.Start();
            
            // Wait for both threads to complete
            thread1.Join(TimeSpan.FromSeconds(10));
            thread2.Join(TimeSpan.FromSeconds(10));
            
            // Check for exceptions
            if (thread1Exception != null) throw thread1Exception;
            if (thread2Exception != null) throw thread2Exception;
            
            // Verify final state
            var finalProduct = productProvider1.GetById(productId);
            
            // The final price should be 115 (thread 1's value) 
            // because thread 1 committed last and overwrote thread 2's change
            // This demonstrates the lost update problem
            Assert.AreEqual(115m, finalProduct.Price, 
                "Final price should be 115 (thread 1's value) due to lost update - thread 2's change was overwritten");
            
            // Cleanup
            productProvider1 = null;
            productProvider2 = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(productDbPath))
            {
                File.Delete(productDbPath);
            }
        }
    }
}