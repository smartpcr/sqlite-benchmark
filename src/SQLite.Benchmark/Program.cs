using System;
using BenchmarkDotNet.Running;
using Serilog;

namespace SQLite.Benchmark
{
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

                var summary = BenchmarkRunner.Run<SqliteProviderBenchmarks>();

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
