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
BENCHMARK_TYPE=""

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
        -t|--type)
            BENCHMARK_TYPE="$2"
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
            echo "  -t, --type <standard|payload|config|all|failover>   Benchmark type (default: interactive)"
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
# Since BaseOutputPath is removed, binaries are now in the project's local bin folder
OUTPUT_PATH="$SCRIPT_DIR/src/SQLite.Benchmark/bin/$CONFIGURATION/net472"

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

# Handle interactive mode if no type specified
if [ -z "$BENCHMARK_TYPE" ]; then
    echo
    echo -e "${CYAN}Select benchmark type to run:${NC}"
    echo "  1. Standard benchmarks (various operations with different record counts)"
    echo "  2. Payload size benchmarks (test with different data sizes)"
    echo "  3. Configuration benchmarks (test different SQLite settings)"
    echo "  4. All benchmarks"
    echo "  5. Failover test (simulate service instance switching)"
    echo
    read -p "Enter your choice (1-5): " choice
    
    case $choice in
        1)
            BENCHMARK_TYPE="standard"
            ;;
        2)
            BENCHMARK_TYPE="payload"
            ;;
        3)
            BENCHMARK_TYPE="config"
            ;;
        4)
            BENCHMARK_TYPE="all"
            ;;
        5)
            BENCHMARK_TYPE="failover"
            ;;
        *)
            echo -e "${RED}Invalid selection. Please run again and choose 1, 2, 3, 4, or 5.${NC}"
            exit 1
            ;;
    esac
    
    echo
    echo -e "${GREEN}Running ${BENCHMARK_TYPE} benchmarks...${NC}"
fi

# Prepare benchmark arguments
BENCHMARK_ARGS=""

# Handle failover test separately
if [ "$BENCHMARK_TYPE" = "failover" ]; then
    echo -e "${YELLOW}Running failover tests...${NC}"
    echo
    
    dotnet test "$SCRIPT_DIR/src/SQLite.Tests/SQLite.Tests.csproj" -c "$CONFIGURATION" --filter "FullyQualifiedName~SqliteFailoverTests|FullyQualifiedName~SqliteDockerFailoverTests"
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Failover tests failed!${NC}"
        exit 1
    fi
    
    echo
    echo -e "${GREEN}Failover tests completed successfully!${NC}"
    exit 0
fi

# Add benchmark type argument
case $BENCHMARK_TYPE in
    payload)
        BENCHMARK_ARGS="--payload"
        ;;
    config)
        BENCHMARK_ARGS="--config"
        ;;
    all)
        BENCHMARK_ARGS="--all"
        ;;
    # standard requires no additional argument
esac

if [ "$FILTER" != "*" ]; then
    BENCHMARK_ARGS="$BENCHMARK_ARGS --filter $FILTER"
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