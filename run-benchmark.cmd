@echo off
REM SQLite Benchmark Runner with interactive selection

echo SQLite Benchmark Runner
echo ======================
echo.

REM Check for command line arguments
if "%1"=="" goto :interactive
if /i "%1"=="standard" goto :standard
if /i "%1"=="payload" goto :payload
if /i "%1"=="config" goto :config
if /i "%1"=="all" goto :all
if /i "%1"=="failover" goto :failover
goto :usage

:interactive
echo Select benchmark type to run:
echo   1. Standard benchmarks (various operations with different record counts)
echo   2. Payload size benchmarks (test with different data sizes)
echo   3. Configuration benchmarks (test different SQLite settings)
echo   4. All benchmarks
echo   5. Failover test (simulate service instance switching)
echo.
set /p choice="Enter your choice (1-5): "

if "%choice%"=="1" goto :standard
if "%choice%"=="2" goto :payload
if "%choice%"=="3" goto :config
if "%choice%"=="4" goto :all
if "%choice%"=="5" goto :failover

echo Invalid selection. Please run again and choose 1, 2, 3, 4, or 5.
exit /b 1

:usage
echo Usage: %0 [standard^|payload^|config^|all^|failover]
echo    or run without arguments for interactive mode
exit /b 1

:standard
set BENCH_ARGS=
echo Running Standard benchmarks...
goto :build

:payload
set BENCH_ARGS=--payload
echo Running Payload size benchmarks...
goto :build

:config
set BENCH_ARGS=--config
echo Running Configuration benchmarks...
goto :build

:all
set BENCH_ARGS=--all
echo Running All benchmarks...
goto :build

:failover
echo.
echo Running Failover tests...
dotnet test src\SQLite.Tests\SQLite.Tests.csproj -c Release --filter "FullyQualifiedName~SqliteFailoverTests|FullyQualifiedName~SqliteDockerFailoverTests"

if %ERRORLEVEL% NEQ 0 (
    echo Failover tests failed!
    exit /b %ERRORLEVEL%
)

echo.
echo Failover tests completed successfully!
echo.
pause
exit /b 0

:build
echo.
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
REM Since BaseOutputPath is removed, binaries are now in the project's local bin folder
cd src\SQLite.Benchmark\bin\Release\net472
SQLite.Benchmark.exe %BENCH_ARGS%

if %ERRORLEVEL% NEQ 0 (
    echo Benchmark failed!
    exit /b %ERRORLEVEL%
)

cd ..\..\..\..\..\

echo.
echo Benchmark completed successfully!
echo Results saved to: BenchmarkDotNet.Artifacts
echo.

pause