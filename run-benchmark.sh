#!/bin/bash

# SQLite Benchmark Runner for Linux/Mac

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
CONFIGURATION="Release"
FILTER="*"
NO_BUILD=0

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -f|--filter)
            FILTER="$2"
            shift 2
            ;;
        --no-build)
            NO_BUILD=1
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  -c, --configuration <Debug|Release>  Build configuration (default: Release)"
            echo "  -f, --filter <pattern>              Filter benchmarks (default: *)"
            echo "  --no-build                          Skip build step"
            echo "  -h, --help                          Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}SQLite Benchmark Runner${NC}"
echo -e "${CYAN}======================${NC}"
echo

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_PATH="$SCRIPT_DIR/src/SQLite.Benchmark/SQLite.Benchmark.csproj"
OUTPUT_PATH="$SCRIPT_DIR/bin/$CONFIGURATION/net472"

# Check if project exists
if [ ! -f "$PROJECT_PATH" ]; then
    echo -e "${RED}Error: Benchmark project not found at: $PROJECT_PATH${NC}"
    exit 1
fi

# Build the project unless skipped
if [ $NO_BUILD -eq 0 ]; then
    echo -e "${YELLOW}Building benchmark project ($CONFIGURATION)...${NC}"
    
    dotnet build "$PROJECT_PATH" -c "$CONFIGURATION" --verbosity minimal
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Build failed!${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}Build completed successfully!${NC}"
    echo
fi

# Check if output exists
EXE_PATH="$OUTPUT_PATH/SQLite.Benchmark.exe"
if [ ! -f "$EXE_PATH" ]; then
    echo -e "${RED}Error: Benchmark executable not found at: $EXE_PATH${NC}"
    echo "Please ensure the project has been built for $CONFIGURATION configuration"
    exit 1
fi

# Prepare benchmark arguments
BENCHMARK_ARGS=""
if [ "$FILTER" != "*" ]; then
    BENCHMARK_ARGS="--filter $FILTER"
fi

# Create results directory
RESULTS_DIR="$SCRIPT_DIR/BenchmarkDotNet.Artifacts"
mkdir -p "$RESULTS_DIR"

echo -e "${YELLOW}Running benchmarks...${NC}"
if [ "$FILTER" != "*" ]; then
    echo -e "${CYAN}Filter: $FILTER${NC}"
fi
echo -e "${CYAN}Results will be saved to: $RESULTS_DIR${NC}"
echo

# Change to output directory
cd "$OUTPUT_PATH"

# Run the benchmark
if command -v mono &> /dev/null; then
    # Use mono if available (for Linux/Mac)
    mono "$EXE_PATH" $BENCHMARK_ARGS
else
    # Try to run directly (might work with .NET Core)
    "$EXE_PATH" $BENCHMARK_ARGS
fi

if [ $? -ne 0 ]; then
    echo -e "${RED}Benchmark failed!${NC}"
    exit 1
fi

cd - > /dev/null

echo
echo -e "${GREEN}Benchmark completed successfully!${NC}"
echo

# Check for results
if [ -d "$RESULTS_DIR" ]; then
    HTML_FILES=$(find "$RESULTS_DIR" -name "*.html" -type f 2>/dev/null | sort -r | head -5)
    if [ -n "$HTML_FILES" ]; then
        echo -e "${CYAN}Results available:${NC}"
        echo "$HTML_FILES" | while read -r file; do
            echo "  - $(basename "$file")"
        done
    fi
fi

echo
echo "Benchmark artifacts location: $RESULTS_DIR"