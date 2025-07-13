# SQLite Benchmark Guide

This project includes comprehensive benchmarks for the SQLite provider implementation.

## Running Benchmarks

### Windows

#### PowerShell (Recommended)
```powershell
.\run-benchmark.ps1
```

Options:
- `-Configuration <Debug|Release>` - Build configuration (default: Release)
- `-Filter <pattern>` - Filter specific benchmarks (e.g., `"*Insert*"`)
- `-NoBuild` - Skip build step

Examples:
```powershell
# Run all benchmarks
.\run-benchmark.ps1

# Run only insert benchmarks
.\run-benchmark.ps1 -Filter "*Insert*"

# Run in Debug mode
.\run-benchmark.ps1 -Configuration Debug
```

#### Command Prompt
```cmd
run-benchmark.cmd
```

### Linux/Mac

```bash
./run-benchmark.sh
```

Options:
- `-c, --configuration <Debug|Release>` - Build configuration (default: Release)
- `-f, --filter <pattern>` - Filter specific benchmarks
- `--no-build` - Skip build step

Examples:
```bash
# Run all benchmarks
./run-benchmark.sh

# Run only insert benchmarks
./run-benchmark.sh -f "*Insert*"

# Run without building
./run-benchmark.sh --no-build
```

## Benchmark Categories

The benchmarks test various aspects of SQLite operations:

### Basic Operations
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

### Parameters
- **RecordCount**: [100, 1000, 10000] - Number of records to test with
- **ThreadCount**: [1, 4, 8] - Number of concurrent threads

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