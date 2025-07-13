#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the SQLite benchmark tests
.DESCRIPTION
    This script builds and runs the SQLite benchmark project with various configurations
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.
.PARAMETER Filter
    Filter for specific benchmarks to run (uses BenchmarkDotNet filter syntax)
.PARAMETER NoBuild
    Skip the build step and run existing binaries
.PARAMETER BenchmarkType
    Type of benchmark to run: Standard, Payload, or All
.EXAMPLE
    .\run-benchmark.ps1
    .\run-benchmark.ps1 -Configuration Debug
    .\run-benchmark.ps1 -Filter "*Insert*"
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [string]$Filter = '*',
    
    [switch]$NoBuild,
    
    [ValidateSet('Standard', 'Payload', 'Config', 'All', 'Interactive')]
    [string]$BenchmarkType = 'Interactive'
)

$ErrorActionPreference = 'Stop'

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir 'src/SQLite.Benchmark/SQLite.Benchmark.csproj'
$outputPath = Join-Path $scriptDir "bin/$Configuration/net472"

Write-Host "SQLite Benchmark Runner" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host ""

# Check if project exists
if (-not (Test-Path $projectPath)) {
    Write-Error "Benchmark project not found at: $projectPath"
    exit 1
}

# Build the project unless skipped
if (-not $NoBuild) {
    Write-Host "Building benchmark project ($Configuration)..." -ForegroundColor Yellow
    
    $buildArgs = @(
        'build',
        $projectPath,
        '-c', $Configuration,
        '--verbosity', 'minimal'
    )
    
    & dotnet $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host ""
}

# Check if output exists
$exePath = Join-Path $outputPath 'SQLite.Benchmark.exe'
if (-not (Test-Path $exePath)) {
    Write-Error "Benchmark executable not found at: $exePath"
    Write-Host "Please ensure the project has been built for $Configuration configuration"
    exit 1
}

# Handle interactive mode
if ($BenchmarkType -eq 'Interactive') {
    Write-Host ""
    Write-Host "Select benchmark type to run:" -ForegroundColor Cyan
    Write-Host "  1. Standard benchmarks (various operations with different record counts)" -ForegroundColor Gray
    Write-Host "  2. Payload size benchmarks (test with different data sizes)" -ForegroundColor Gray
    Write-Host "  3. Configuration benchmarks (test different SQLite settings)" -ForegroundColor Gray
    Write-Host "  4. All benchmarks" -ForegroundColor Gray
    Write-Host ""
    
    $selection = Read-Host "Enter your choice (1-4)"
    
    switch ($selection) {
        '1' { $BenchmarkType = 'Standard' }
        '2' { $BenchmarkType = 'Payload' }
        '3' { $BenchmarkType = 'Config' }
        '4' { $BenchmarkType = 'All' }
        default {
            Write-Error "Invalid selection. Please run again and choose 1, 2, 3, or 4."
            exit 1
        }
    }
    
    Write-Host ""
    Write-Host "Running $BenchmarkType benchmarks..." -ForegroundColor Green
}

# Prepare benchmark arguments
$benchmarkArgs = @()

# Add benchmark type argument
switch ($BenchmarkType) {
    'Payload' {
        $benchmarkArgs += '--payload'
    }
    'Config' {
        $benchmarkArgs += '--config'
    }
    'All' {
        $benchmarkArgs += '--all'
    }
    # 'Standard' requires no additional argument
}

if ($Filter -ne '*') {
    $benchmarkArgs += '--filter'
    $benchmarkArgs += $Filter
}

# Add additional BenchmarkDotNet arguments for better output
$benchmarkArgs += '--info'

# Create results directory
$resultsDir = Join-Path $scriptDir 'BenchmarkDotNet.Artifacts'
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir | Out-Null
}

Write-Host "Running benchmarks..." -ForegroundColor Yellow
if ($Filter -ne '*') {
    Write-Host "Filter: $Filter" -ForegroundColor Cyan
}
Write-Host "Results will be saved to: $resultsDir" -ForegroundColor Cyan
Write-Host ""

# Change to output directory to ensure proper working directory
Push-Location $outputPath

try {
    # Run the benchmark
    & $exePath $benchmarkArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Benchmark failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Benchmark completed successfully!" -ForegroundColor Green
Write-Host ""

# Check for results
$resultFiles = Get-ChildItem -Path $resultsDir -Filter "*.html" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending

if ($resultFiles) {
    Write-Host "Results available:" -ForegroundColor Cyan
    foreach ($file in $resultFiles | Select-Object -First 5) {
        Write-Host "  - $($file.Name)" -ForegroundColor Gray
    }
    
    # Open the latest HTML report if available
    $latestHtml = $resultFiles | Select-Object -First 1
    if ($latestHtml) {
        Write-Host ""
        $openReport = Read-Host "Would you like to open the latest HTML report? (Y/N)"
        if ($openReport -eq 'Y' -or $openReport -eq 'y') {
            Start-Process $latestHtml.FullName
        }
    }
}

Write-Host ""
Write-Host "Benchmark artifacts location: $resultsDir" -ForegroundColor Gray