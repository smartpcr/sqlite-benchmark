# SQLite Benchmark Solution

A comprehensive benchmarking solution for SQLite operations using System.Data.SQLite on .NET Framework 4.6.2.

## Overview

This solution provides a generic SQLite provider with full CRUD operations, comprehensive unit tests, and detailed performance benchmarks. It's designed to measure SQLite performance for various scenarios including single operations, batch operations, and concurrent access patterns.

## Project Structure

```
sqlite-benchmark/
├── src/
│   ├── SQLite.Lib/          # Core library with generic SQLite provider
│   ├── SQLite.Tests/        # MSTest unit tests
│   └── SQLite.Benchmark/    # BenchmarkDotNet performance tests
├── docs/                    # Documentation
├── .editorconfig           # Code style configuration
├── Directory.Build.props    # Common build properties
├── Directory.Packages.props # Central package management
└── SQLiteBenchmark.sln     # Solution file
```

## Features

### SQLite.Lib
- Generic `SqliteProvider<T>` for type-safe CRUD operations
- JSON serialization for complex object storage
- Transaction support with automatic rollback
- Connection pooling and management
- Custom query execution
- Performance optimizations (WAL mode, prepared statements)

### SQLite.Tests
- Comprehensive unit test coverage
- Concurrency tests
- Transaction tests
- Performance tests for large datasets
- Data-driven test scenarios

### SQLite.Benchmark
- BenchmarkDotNet integration
- Memory usage diagnostics
- Threading diagnostics
- Configurable test parameters (record count, thread count)
- Various benchmark scenarios:
  - Single insert/update/delete
  - Batch operations
  - Concurrent reads/writes
  - Complex queries
  - Transaction performance

## Prerequisites

- .NET Framework 4.6.2 or higher
- Visual Studio 2017 or higher (for SDK-style projects)
- Windows OS (for BenchmarkDotNet.Diagnostics.Windows)

## Getting Started

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd sqlite-benchmark
   ```

2. Restore packages:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

5. Run benchmarks:
   ```bash
   cd src/SQLite.Benchmark
   dotnet run -c Release
   ```

## Usage Example

```csharp
// Create provider
var connectionString = "Data Source=mydb.db;Version=3;";
var provider = new SqliteProvider<MyEntity>(connectionString);

// Create table
provider.CreateTable();

// Insert entity
var entity = new MyEntity { Name = "Test", Value = 42 };
var saved = provider.Insert(entity);

// Query entities
var active = provider.Find(e => e.IsActive).ToList();

// Batch operations with transaction
using (provider.BeginTransaction())
{
    provider.InsertBatch(entities);
}
```

## Benchmark Results

The benchmark suite measures:
- [basic](https://raw.githack.com/smartpcr/sqlite-benchmark/main/docs/benchmark-results/SQLite.Benchmark.PayloadSize.Report.html)
- [payload size](https://raw.githack.com/smartpcr/sqlite-benchmark/main/docs/benchmark-results/SQLite.Benchmark.PayloadSize.html)


Results are output in various formats and saved to the `BenchmarkDotNet.Artifacts` folder.


## Acknowledgments

- System.Data.SQLite by SQLite Development Team
- BenchmarkDotNet for performance testing
- Serilog for structured logging
