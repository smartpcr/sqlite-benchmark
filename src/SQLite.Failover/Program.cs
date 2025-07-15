using System;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using CommandLine;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SQLite.Lib;

namespace SQLite.Failover
{
    class Program
    {
        public class Options
        {
            [Option('d', "database", Required = true, HelpText = "Path to SQLite database file")]
            public string DatabasePath { get; set; }

            [Option('i', "instance", Required = true, HelpText = "Instance name (e.g., instance1, instance2)")]
            public string InstanceName { get; set; }

            [Option('l', "loops", Required = true, HelpText = "Number of loops to run")]
            public int Loops { get; set; }

            [Option('m', "mode", Required = true, HelpText = "Mode: 'init' to initialize with products, 'update' to update prices")]
            public string Mode { get; set; }

            [Option('p', "products", Default = 100, HelpText = "Number of products to create (only for 'init' mode)")]
            public int ProductCount { get; set; }

            [Option('r', "retry-timeout", Default = 30000, HelpText = "Retry timeout in milliseconds for acquiring lock")]
            public int RetryTimeout { get; set; }

            [Option('w', "wait-before", Default = 0, HelpText = "Wait time in milliseconds before starting")]
            public int WaitBefore { get; set; }

            [Option('s', "simulate-crash", Default = false, HelpText = "Simulate crash after completing loops")]
            public bool SimulateCrash { get; set; }
        }

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    opts => RunInstance(opts),
                    errs => 1);
        }

        static int RunInstance(Options options)
        {
            // Setup logging
            var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File($"failover-{options.InstanceName}.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var loggerFactory = new SerilogLoggerFactory(logger);
            var instanceLogger = loggerFactory.CreateLogger<SqliteProvider<Product>>();

            logger.Information("Starting {Instance} - Mode: {Mode}, Loops: {Loops}, Database: {Database}",
                options.InstanceName, options.Mode, options.Loops, options.DatabasePath);

            try
            {
                // Wait if specified
                if (options.WaitBefore > 0)
                {
                    logger.Information("Waiting {WaitTime}ms before starting...", options.WaitBefore);
                    Thread.Sleep(options.WaitBefore);
                }

                // Create connection string with retry timeout
                var connectionString = $"Data Source={options.DatabasePath};Version=3;Journal Mode=WAL;Busy Timeout={options.RetryTimeout};";

                var provider = new SqliteProvider<Product>(connectionString, instanceLogger);

                if (options.Mode.ToLower() == "init")
                {
                    InitializeDatabase(provider, options.ProductCount, logger);
                }
                else if (options.Mode.ToLower() == "update")
                {
                    UpdatePrices(provider, options.Loops, options.InstanceName, logger);
                }
                else
                {
                    logger.Error("Invalid mode: {Mode}. Use 'init' or 'update'", options.Mode);
                    return 1;
                }

                if (options.SimulateCrash)
                {
                    logger.Warning("{Instance} simulating crash!", options.InstanceName);
                    Environment.Exit(99); // Special exit code for simulated crash
                }

                logger.Information("{Instance} completed successfully", options.InstanceName);
                return 0;
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy)
            {
                logger.Error("Failed to acquire database lock after {Timeout}ms: {Message}",
                    options.RetryTimeout, ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unexpected error in {Instance}", options.InstanceName);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static void InitializeDatabase(SqliteProvider<Product> provider, int productCount, Serilog.ILogger logger)
        {
            logger.Information("Initializing database with {Count} products", productCount);

            // Create table if not exists
            provider.CreateTable();

            // Clear existing data
            var existingProducts = provider.GetAll().ToList();
            if (existingProducts.Any())
            {
                logger.Warning("Clearing {Count} existing products", existingProducts.Count);
                foreach (var product in existingProducts)
                {
                    provider.Delete(product.Id);
                }
            }

            // Add new products
            using (var transaction = provider.BeginTransaction())
            {
                for (int i = 1; i <= productCount; i++)
                {
                    var product = new Product
                    {
                        Id = i,
                        Name = $"Product {i}",
                        Price = 10.0m // Initial price
                    };
                    provider.Insert(product);
                }
            }

            logger.Information("Successfully initialized {Count} products", productCount);
        }

        static void UpdatePrices(SqliteProvider<Product> provider, int loops, string instanceName, Serilog.ILogger logger)
        {
            logger.Information("{Instance} starting price updates for {Loops} loops", instanceName, loops);

            for (int loop = 1; loop <= loops; loop++)
            {
                var retryCount = 0;
                const int maxRetries = 10;
                var success = false;

                while (!success && retryCount < maxRetries)
                {
                    try
                    {
                        using (var transaction = provider.BeginTransaction())
                        {
                            var products = provider.GetAll().ToList();
                            logger.Information("{Instance} - Loop {Loop}/{Total}: Updating {Count} products",
                                instanceName, loop, loops, products.Count);

                            foreach (var product in products)
                            {
                                product.Price += 1.0m; // Increment by $1
                                provider.Update(product);
                            }
                        }

                        logger.Information("{Instance} - Loop {Loop}/{Total}: Completed successfully",
                            instanceName, loop, loops);
                        success = true;
                    }
                    catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy)
                    {
                        retryCount++;
                        logger.Warning("{Instance} - Loop {Loop}: Database locked, retry {Retry}/{Max}",
                            instanceName, loop, retryCount, maxRetries);

                        Thread.Sleep(1000 * retryCount); // Exponential backoff
                    }
                }

                if (!success)
                {
                    throw new InvalidOperationException($"Failed to complete loop {loop} after {maxRetries} retries");
                }

                // Simulate some processing time between loops
                Thread.Sleep(500);
            }

            // Log final state
            var finalProducts = provider.GetAll().Take(5).ToList();
            foreach (var product in finalProducts)
            {
                logger.Information("{Instance} - Final price for {Name}: ${Price}",
                    instanceName, product.Name, product.Price);
            }
        }
    }
}
