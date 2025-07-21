// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Benchmark
{
    using System;
    using BenchmarkDotNet.Running;
    using Serilog;
    using Serilog.Events;

    class Program
    {
        static void Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("benchmark-log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Starting SQLite Benchmark");

                // Run different benchmark suites based on command line arguments
                if (args.Length > 0 && args[0] == "--payload")
                {
                    Log.Information("Running Payload Size Benchmarks");
                    var summary = BenchmarkRunner.Run<PayloadSizeBenchmarks>();
                }
                else if (args.Length > 0 && args[0] == "--config")
                {
                    Log.Information("Running SQLite Configuration Benchmarks");
                    var summary = BenchmarkRunner.Run<SqliteConfigurationBenchmarks>();
                }
                else if (args.Length > 0 && args[0] == "--all")
                {
                    Log.Information("Running All Benchmarks");
                    BenchmarkRunner.Run<SqliteProviderBenchmarks>();
                    BenchmarkRunner.Run<PayloadSizeBenchmarks>();
                    BenchmarkRunner.Run<SqliteConfigurationBenchmarks>();
                }
                else
                {
                    Log.Information("Running Standard Benchmarks");
                    var summary = BenchmarkRunner.Run<SqliteProviderBenchmarks>();
                    Log.Information("");
                    Log.Information("Tip: Use --payload to run payload size benchmarks");
                    Log.Information("     Use --config to run configuration benchmarks");
                    Log.Information("     Use --all to run all benchmark suites");
                }

                Log.Information("Benchmark completed successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Benchmark failed");
                Environment.Exit(1);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
