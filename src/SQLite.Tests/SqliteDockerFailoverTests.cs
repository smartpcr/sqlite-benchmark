using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Extensions.Logging;
using SQLite.Lib;

namespace SQLite.Tests
{
    [TestClass]
    public class SqliteDockerFailoverTests
    {
        private string databasePath;
        private string dataDirectory;
        private ILogger<PersistenceProvider<Product>> logger;
        private ILoggerFactory loggerFactory;

        [TestInitialize]
        public void Initialize()
        {
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            this.loggerFactory = new SerilogLoggerFactory(serilogLogger);
            this.logger = this.loggerFactory.CreateLogger<PersistenceProvider<Product>>();

            // Create data directory
            this.dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
            if (!Directory.Exists(this.dataDirectory))
            {
                Directory.CreateDirectory(this.dataDirectory);
            }

            this.databasePath = Path.Combine(this.dataDirectory, "failover.db");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up Docker containers
            this.logger.LogInformation("Cleaning up Docker containers...");
            RunCommand("docker-compose", "-f docker-compose.failover.yml down -v");

            // Clean up database files
            if (Directory.Exists(this.dataDirectory))
            {
                try
                {
                    Directory.Delete(this.dataDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [TestMethod]
        [TestCategory("Docker")]
        public async Task DockerFailoverTest_ShouldHandleProcessLevelLocking()
        {
            this.logger.LogInformation("Starting Docker-based failover test");

            // Build Docker image
            this.logger.LogInformation("Building Docker image...");
            var buildResult = RunCommand("docker", "build -f Dockerfile.failover -t sqlite-failover .");
            if (buildResult.ExitCode != 0)
            {
                Assert.Fail($"Failed to build Docker image: {buildResult.Error}");
            }

            // Start init container
            this.logger.LogInformation("Initializing database...");
            var initResult = RunCommand("docker-compose", "-f docker-compose.failover.yml run --rm init");
            if (initResult.ExitCode != 0)
            {
                Assert.Fail($"Failed to initialize database: {initResult.Error}");
            }

            // Start instance1 in background (it will crash after 3 loops)
            this.logger.LogInformation("Starting instance1 (will crash after 3 loops)...");
            var instance1Process = StartProcess("docker-compose", "-f docker-compose.failover.yml run --rm instance1");

            // Wait a bit for instance1 to start and process some updates
            await Task.Delay(5000);

            // Start instance2 (it will wait for the lock and then take over)
            this.logger.LogInformation("Starting instance2 (will wait for lock and take over)...");
            var instance2Process = StartProcess("docker-compose", "-f docker-compose.failover.yml run --rm instance2");

            // Wait for instance1 to crash
            this.logger.LogInformation("Waiting for instance1 to crash...");
            instance1Process.WaitForExit(30000);
            var instance1Output = instance1Process.StandardOutput.ReadToEnd();
            var instance1Error = instance1Process.StandardError.ReadToEnd();

            this.logger.LogInformation("Instance1 exit code: {ExitCode}", instance1Process.ExitCode);
            this.logger.LogInformation("Instance1 output: {Output}", instance1Output);

            Assert.AreEqual(99, instance1Process.ExitCode, "Instance1 should exit with crash code 99");
            Assert.IsTrue(instance1Output.Contains("Simulating crash") || instance1Error.Contains("Simulating crash"),
                "Instance1 should simulate crash");

            // Wait for instance2 to complete
            this.logger.LogInformation("Waiting for instance2 to complete...");
            instance2Process.WaitForExit(60000);
            var instance2Output = instance2Process.StandardOutput.ReadToEnd();

            this.logger.LogInformation("Instance2 exit code: {ExitCode}", instance2Process.ExitCode);
            this.logger.LogInformation("Instance2 output: {Output}", instance2Output);

            Assert.AreEqual(0, instance2Process.ExitCode, "Instance2 should complete successfully");
            Assert.IsTrue(instance2Output.Contains("Successfully acquired lock") || instance2Output.Contains("Completed update"),
                "Instance2 should successfully take over");

            // Verify final state
            VerifyFinalState();
        }

        [TestMethod]
        [TestCategory("Process")]
        public void ProcessFailoverTest_WithoutDocker()
        {
            this.logger.LogInformation("Starting process-based failover test (without Docker)");

            var exePath = Path.Combine(
                FindRepoRoot(),
                "src",
                "SQLite.Failover",
                "bin",
                "Release",
                "net472",
                "SQLite.Failover.exe");

            if (!File.Exists(exePath))
            {
                // Try to build it first
                this.logger.LogInformation("Building SQLite.Failover project...");
                var buildResult = RunCommand("dotnet", "build src/SQLite.Failover/SQLite.Failover.csproj -c Release");
                if (buildResult.ExitCode != 0)
                {
                    Assert.Inconclusive($"SQLite.Failover.exe not found and build failed: {buildResult.Error}");
                }
            }

            // Initialize database
            var initArgs = $"-d \"{this.databasePath}\" -i init -l 0 -m init -p 100";
            var initResult = RunCommand(exePath, initArgs);
            Assert.AreEqual(0, initResult.ExitCode, $"Init failed: {initResult.Error}");

            // Start instance1 (will run 3 loops and crash)
            var instance1Args = $"-d \"{this.databasePath}\" -i instance1 -l 3 -m update -s";
            var instance1Process = StartProcess(exePath, instance1Args);

            // Wait for instance1 to complete (it should exit with code 99)
            instance1Process.WaitForExit(30000);
            Assert.AreEqual(99, instance1Process.ExitCode, "Instance1 should exit with crash code 99");

            // Wait a bit before starting instance2
            Thread.Sleep(2000);

            // Start instance2 (will run 5 loops)
            var instance2Args = $"-d \"{this.databasePath}\" -i instance2 -l 5 -m update";
            var instance2Result = RunCommand(exePath, instance2Args);
            Assert.AreEqual(0, instance2Result.ExitCode, $"Instance2 failed: {instance2Result.Error}");

            // Verify final state
            VerifyFinalState();
        }

        private void VerifyFinalState()
        {
            this.logger.LogInformation("Verifying final state and consistency...");

            var connectionString = $"Data Source={this.databasePath};Version=3;";
            var provider = new PersistenceProvider<Product>(connectionString, this.logger);
            var products = provider.GetAll().OrderBy(p => p.Id).ToList();

            Assert.AreEqual(100, products.Count, "Should have 100 products");

            // Initial price: $10
            // Instance1: 3 loops (+$3)
            // Instance2: 5 loops (+$5)
            // Total: $18
            var expectedPrice = 18.0m;

            // Check consistency - all products should have the same price
            var prices = products.Select(p => p.Price).Distinct().ToList();
            Assert.AreEqual(1, prices.Count, "All products should have the same price (consistency check)");

            // Check the price is correct
            var actualPrice = prices.First();
            Assert.AreEqual(expectedPrice,
                actualPrice,
                $"Price mismatch. Expected: ${expectedPrice}, Actual: ${actualPrice}");

            // Log sample products for verification
            foreach (var product in products.Take(10))
            {
                this.logger.LogInformation("Product {Id}: ${Price}", product.Id, product.Price);
            }

            this.logger.LogInformation("Verification passed: All {Count} products have consistent price of ${Price}",
                products.Count,
                expectedPrice);

            // Additional consistency checks
            this.logger.LogInformation("Running additional consistency checks...");

            // Check database integrity
            var integrityCheck = provider.ExecuteScalar<string>("PRAGMA integrity_check;");
            Assert.AreEqual("ok", integrityCheck, "Database integrity check failed");

            // Check WAL mode is still enabled
            var journalMode = provider.ExecuteScalar<string>("PRAGMA journal_mode;");
            Assert.AreEqual("wal", journalMode.ToLower(), "Journal mode should remain WAL after failover");

            this.logger.LogInformation("All consistency checks passed");
        }

        private ProcessResult RunCommand(string command, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = FindRepoRoot()
            };

            using var process = Process.Start(startInfo);
            var output = process!.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            this.logger.LogDebug("Command: {Command} {Arguments}", command, arguments);
            this.logger.LogDebug("Exit Code: {ExitCode}", process.ExitCode);
            if (!string.IsNullOrEmpty(output))
            {
                this.logger.LogDebug("Output: {Output}", output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                this.logger.LogDebug("Error: {Error}", error);
            }

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }
            if (dir == null)
                throw new DirectoryNotFoundException("Repository root with .git folder not found.");
            return dir.FullName;
        }

        private Process StartProcess(string command, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            this.logger.LogInformation("Starting process: {Command} {Arguments}", command, arguments);
            return Process.Start(startInfo);
        }

        private class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
    }
}
