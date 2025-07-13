# SQLite Benchmark Guide

This project includes comprehensive benchmarks for the SQLite provider implementation.

## Running Benchmarks

### Windows

#### PowerShell (Recommended)
```powershell
.\run-benchmark.ps1
```

When run without parameters, it will prompt you to select the benchmark type interactively.

Options:
- `-Configuration <Debug|Release>` - Build configuration (default: Release)
- `-Filter <pattern>` - Filter specific benchmarks (e.g., `"*Insert*"`)
- `-NoBuild` - Skip build step
- `-BenchmarkType <Standard|Payload|Config|All|Interactive>` - Choose benchmark suite (default: Interactive)

Examples:
```powershell
# Interactive mode - prompts for benchmark type
.\run-benchmark.ps1

# Run standard benchmarks directly
.\run-benchmark.ps1 -BenchmarkType Standard

# Run payload size benchmarks
.\run-benchmark.ps1 -BenchmarkType Payload

# Run configuration benchmarks
.\run-benchmark.ps1 -BenchmarkType Config

# Run all benchmark suites
.\run-benchmark.ps1 -BenchmarkType All

# Run only insert benchmarks
.\run-benchmark.ps1 -Filter "*Insert*"

# Run payload benchmarks with filter
.\run-benchmark.ps1 -BenchmarkType Payload -Filter "*Insert*"

# Run in Debug mode
.\run-benchmark.ps1 -Configuration Debug
```

#### Command Prompt
```cmd
REM Interactive mode - prompts for benchmark type
run-benchmark.cmd

REM Run specific benchmark type
run-benchmark.cmd standard
run-benchmark.cmd payload
run-benchmark.cmd config
run-benchmark.cmd all
```

### Linux/Mac

```bash
./run-benchmark.sh
```

When run without parameters, it will prompt you to select the benchmark type interactively.

Options:
- `-c, --configuration <Debug|Release>` - Build configuration (default: Release)
- `-f, --filter <pattern>` - Filter specific benchmarks
- `-t, --type <standard|payload|config|all>` - Benchmark type (default: interactive)
- `--no-build` - Skip build step

Examples:
```bash
# Interactive mode - prompts for benchmark type
./run-benchmark.sh

# Run standard benchmarks directly
./run-benchmark.sh -t standard

# Run payload size benchmarks
./run-benchmark.sh -t payload

# Run configuration benchmarks
./run-benchmark.sh -t config

# Run all benchmarks
./run-benchmark.sh -t all

# Run only insert benchmarks
./run-benchmark.sh -f "*Insert*"

# Run payload benchmarks with filter
./run-benchmark.sh -t payload -f "*Insert*"

# Run without building
./run-benchmark.sh --no-build
```

## Benchmark Categories

### Standard Benchmarks (`SqliteProviderBenchmarks`)

Tests various aspects of SQLite operations:

#### Basic Operations
- **SingleInsert** - Insert a single record
- **BatchInsert** - Insert multiple records in a batch
- **SingleSelect** - Select a single record by ID
- **SelectAll** - Select all records
- **SelectWithFilter** - Select records with filtering
- **SingleUpdate** - Update a single record
- **SingleDelete** - Delete a single record
- **CountAll** - Count all records

### Advanced Operations
- **TransactionBatchInsert** - Insert multiple records within a transaction
- **ConcurrentReads** - Concurrent read operations
- **ConcurrentWrites** - Concurrent write operations
- **ComplexQuery** - Complex JSON-based queries

#### Parameters
- **RecordCount**: [100, 1000, 10000] - Number of records to test with
- **ThreadCount**: [1, 4, 8] - Number of concurrent threads

### Payload Size Benchmarks (`PayloadSizeBenchmarks`)

Tests SQLite performance with different data sizes:

#### Operations
- **Insert** - Insert a single record with specified payload size
- **BatchInsert** - Insert multiple records with specified payload size
- **Select** - Select a record with specified payload size
- **Update** - Update a record with specified payload size
- **Delete** - Delete a record with specified payload size
- **TransactionInsert** - Insert multiple records in a transaction
- **SelectMultiple** - Select multiple records with specified payload size

#### Payload Sizes
- **ExtraSmall**: 150 bytes
- **Small**: 1 KB
- **Medium**: 100 KB  
- **Large**: 5 MB
- **ExtraLarge**: 50 MB

Note: Large payload benchmarks (5 MB and 50 MB) may take significant time and memory.

### Configuration Benchmarks (`SqliteConfigurationBenchmarks`)

Tests SQLite performance with different configuration settings:

#### Operations
- **SequentialInserts** - Sequential insert operations
- **BatchInsertWithTransaction** - Batch inserts within a transaction
- **SequentialReads** - Sequential read operations by ID
- **BulkRead** - Read all records
- **FilteredRead** - Read with filtering conditions
- **MixedOperations** - Mix of CRUD operations
- **ConcurrentOperations** - Concurrent read/write operations
- **ComplexJsonQuery** - Complex JSON-based queries

#### Configuration Parameters
- **CacheSize**: [1KB, 4KB, 1MB, 4MB] - SQLite page cache size
- **PageSize**: [1KB, 4KB] - Database page size
- **JournalMode**: [WAL, DELETE, MEMORY] - Journal mode for transactions
- **SynchronousMode**: [OFF, NORMAL, FULL] - Synchronization level
- **BusyTimeout**: [100ms, 5s, 30s] - Timeout for locked database
- **EnableForeignKeys**: [true, false] - Foreign key constraint enforcement

## Benchmark Results

Pre-run benchmark results are available in the [docs/benchmark-results](docs/benchmark-results) directory:

### Available Results
- [Payload Size Benchmark Results (HTML)](docs/benchmark-results/SQLite.Benchmark.PayloadSize.Report.html)
- [Payload Size Benchmark Results (Markdown)](docs/benchmark-results/SQLite.Benchmark.PayloadSize.md)
- [Payload Size Benchmark Results (CSV)](docs/benchmark-results/SQLite.Benchmark.PayloadSize.csv)
- [Extra Small Payload Results (HTML)](docs/benchmark-results/SQLite.Benchmark.Basic.Report.ExtraSmall.html)
- [Extra Small Payload Results (Markdown)](docs/benchmark-results/SQLite.Benchmark.Basic.ExtraSmall.md)

## Understanding Results

BenchmarkDotNet will generate detailed reports including:
- **Mean** - Average execution time
- **Error** - Half of 99.9% confidence interval
- **StdDev** - Standard deviation of all measurements
- **Median** - Value separating the higher half from the lower half
- **Allocated** - Allocated memory per single operation

Results are saved in the `BenchmarkDotNet.Artifacts` directory as:
- HTML reports (viewable in browser)
- CSV files (for further analysis)
- Markdown reports

## Tips for Accurate Benchmarks

1. **Close unnecessary applications** to reduce system noise
2. **Run on Release mode** for accurate performance measurements
3. **Ensure consistent system state** between runs
4. **Use the same hardware** when comparing results
5. **Run multiple iterations** - BenchmarkDotNet handles this automatically

## Analyzing Results

Look for:
- **Scalability** - How performance changes with different RecordCount values
- **Concurrency** - Performance impact of multiple threads
- **Memory usage** - Allocated memory per operation
- **Consistency** - Standard deviation indicates consistency

## Troubleshooting

### Build Errors
- Ensure .NET Framework 4.7.2 is installed
- Run `dotnet restore` to restore packages
- Check for missing dependencies

### Runtime Errors
- Ensure SQLite native libraries are available
- Check file permissions for database creation
- Verify sufficient disk space

### Performance Issues
- Disable antivirus scanning for benchmark directory
- Ensure system is not in power-saving mode
- Close resource-intensive applications