#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the SQLite failover test using Docker containers
.DESCRIPTION
    This script builds and runs Docker containers to simulate failover between service instances
.PARAMETER Clean
    Clean up Docker containers and volumes before running
.PARAMETER SkipBuild
    Skip building the Docker image
.EXAMPLE
    .\run-failover-test.ps1
    .\run-failover-test.ps1 -Clean
#>

param(
    [switch]$Clean,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

Write-Host "SQLite Failover Test Runner" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Create data directory
$dataDir = Join-Path $scriptDir "data"
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir | Out-Null
    Write-Host "Created data directory: $dataDir" -ForegroundColor Yellow
}

# Clean up if requested
if ($Clean) {
    Write-Host "Cleaning up existing containers and data..." -ForegroundColor Yellow
    
    # Stop and remove containers
    docker-compose -f docker-compose.failover.yml down 2>$null
    
    # Remove database file
    $dbPath = Join-Path $dataDir "failover.db"
    if (Test-Path $dbPath) {
        Remove-Item $dbPath -Force
        Write-Host "Removed existing database file" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

# Build Docker image
if (-not $SkipBuild) {
    Write-Host "Building Docker image..." -ForegroundColor Yellow
    
    docker build -f Dockerfile.failover -t sqlite-failover .
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build Docker image"
        exit 1
    }
    
    Write-Host "Docker image built successfully!" -ForegroundColor Green
    Write-Host ""
}

# Run the failover scenario
Write-Host "Starting failover scenario..." -ForegroundColor Yellow
Write-Host "  1. Initialize database with 100 products" -ForegroundColor Gray
Write-Host "  2. Instance1 will update prices for 3 loops then crash" -ForegroundColor Gray
Write-Host "  3. Instance2 will wait, then take over for 5 more loops" -ForegroundColor Gray
Write-Host ""

# Run with timestamps
$env:COMPOSE_PROJECT_NAME = "sqlite-failover"
docker-compose -f docker-compose.failover.yml up --abort-on-container-exit

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Docker Compose exited with code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Checking results..." -ForegroundColor Yellow

# Show logs from each instance
Write-Host ""
Write-Host "Instance1 logs (last 10 lines):" -ForegroundColor Cyan
docker-compose -f docker-compose.failover.yml logs --tail=10 instance1

Write-Host ""
Write-Host "Instance2 logs (last 10 lines):" -ForegroundColor Cyan
docker-compose -f docker-compose.failover.yml logs --tail=10 instance2

# Verify the database
$dbPath = Join-Path $dataDir "failover.db"
if (Test-Path $dbPath) {
    Write-Host ""
    Write-Host "Database file exists at: $dbPath" -ForegroundColor Green
    
    # Get file size
    $fileInfo = Get-Item $dbPath
    Write-Host "Database size: $($fileInfo.Length / 1KB) KB" -ForegroundColor Gray
} else {
    Write-Error "Database file not found at: $dbPath"
}

Write-Host ""
Write-Host "Failover test completed!" -ForegroundColor Green
Write-Host ""
Write-Host "To clean up containers and data, run: .\run-failover-test.ps1 -Clean" -ForegroundColor Gray