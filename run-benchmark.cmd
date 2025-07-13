@echo off
REM Simple batch script to run SQLite benchmarks

echo SQLite Benchmark Runner
echo ======================
echo.

REM Build in Release mode
echo Building benchmark project (Release)...
dotnet build src\SQLite.Benchmark\SQLite.Benchmark.csproj -c Release --verbosity minimal

if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b %ERRORLEVEL%
)

echo Build completed successfully!
echo.

REM Run the benchmark
echo Running benchmarks...
cd bin\Release\net472
SQLite.Benchmark.exe

if %ERRORLEVEL% NEQ 0 (
    echo Benchmark failed!
    exit /b %ERRORLEVEL%
)

cd ..\..\..\..

echo.
echo Benchmark completed successfully!
echo Results saved to: BenchmarkDotNet.Artifacts
echo.

pause