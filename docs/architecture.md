# SQLite Benchmark Solution Architecture

## Overview
This solution benchmarks SQLite performance for generic object storage using System.Data.SQLite on .NET Framework 4.6.2.

## Project Structure
```
E:\work\github\scratch\sqlite-benchmark\
├── docs\
│   ├── architecture.md (this file)
│   ├── design-decisions.md
│   └── best-practices.md
├── src\
│   ├── SQLite.Lib\
│   ├── SQLite.Tests\
│   └── SQLite.Benchmark\
├── .editorconfig
├── Directory.Build.props
├── Directory.Packages.props
├── SQLiteBenchmark.sln
└── README.md
```

## Projects

### 1. SQLite.Lib
- **Purpose**: Core library for SQLite operations
- **Key Class**: `SqliteProvider<T>` - Generic provider for CRUD operations
- **Dependencies**: System.Data.SQLite (1.0.119)
- **Target**: .NET Framework 4.6.2

### 2. SQLite.Tests
- **Purpose**: Unit tests for SQLite.Lib
- **Framework**: MSTest
- **Coverage**: All CRUD operations, concurrency scenarios
- **Target**: .NET Framework 4.6.2

### 3. SQLite.Benchmark
- **Purpose**: Performance benchmarking
- **Framework**: BenchmarkDotNet
- **Metrics**: Throughput, latency, memory usage, concurrency
- **Target**: .NET Framework 4.6.2

## Technology Stack
- **Framework**: .NET Framework 4.6.2 (using SDK-style projects)
- **Database**: SQLite via System.Data.SQLite
- **Testing**: MSTest v2
- **Benchmarking**: BenchmarkDotNet
- **Package Management**: Central Package Management (Directory.Packages.props)

## Key Design Patterns
1. **Generic Repository Pattern**: `SqliteProvider<T>`
2. **Unit of Work**: Transaction management
3. **Factory Pattern**: Connection management
4. **Strategy Pattern**: Serialization strategies

## Architecture Decisions
- SDK-style projects for better dependency management
- Central package management for version consistency
- Generic provider for type-safe operations
- Separate benchmark project for performance isolation