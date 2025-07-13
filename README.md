# SQLite Benchmark

This project is a starting point for measuring SQLite concurrency and performance using C#. The benchmarks rely on the `System.Data.SQLite` package and target .NET Framework **4.6.2** so they can run on Windows without requiring the .NET runtime to be installed separately.

The goal is to create repeatable tests that explore how SQLite performs under different access patterns and thread contention scenarios. A minimal solution and test project skeleton are provided to help you begin writing your own benchmarks.

## Windows setup

1. Install **.NET Framework 4.6.2** (included with Visual Studio 2017 or later). The tests use the classic framework rather than .NET Core.
2. Install **Visual Studio** or the [.NET SDK](https://dotnet.microsoft.com/download) if you prefer the command line.
3. Clone this repository and open `SqliteBenchmark.sln`.
4. Restore NuGet packages and build the solution.
5. Run the tests either from the Visual Studio *Test Explorer* or by executing:

   ```
   dotnet test
   ```

   from the repository directory.

## Directory layout

- `SqliteBenchmark.sln` – Visual Studio solution file.
- `tests/SqliteBenchmark.Tests/` – NUnit test project containing the benchmark skeleton.

Feel free to expand the tests with your own performance and concurrency scenarios.
