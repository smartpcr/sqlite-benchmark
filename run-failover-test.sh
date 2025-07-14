#!/bin/bash

# SQLite Failover Test Runner for Linux/Mac

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Default values
CLEAN=0
SKIP_BUILD=0

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --clean)
            CLEAN=1
            shift
            ;;
        --skip-build)
            SKIP_BUILD=1
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --clean       Clean up Docker containers and volumes before running"
            echo "  --skip-build  Skip building the Docker image"
            echo "  -h, --help    Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}SQLite Failover Test Runner${NC}"
echo -e "${CYAN}===========================${NC}"
echo

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DATA_DIR="$SCRIPT_DIR/data"

# Create data directory
if [ ! -d "$DATA_DIR" ]; then
    mkdir -p "$DATA_DIR"
    echo -e "${YELLOW}Created data directory: $DATA_DIR${NC}"
fi

# Clean up if requested
if [ $CLEAN -eq 1 ]; then
    echo -e "${YELLOW}Cleaning up existing containers and data...${NC}"
    
    # Stop and remove containers
    docker-compose -f docker-compose.failover.yml down 2>/dev/null || true
    
    # Remove database file
    DB_PATH="$DATA_DIR/failover.db"
    if [ -f "$DB_PATH" ]; then
        rm -f "$DB_PATH"
        echo -e "${YELLOW}Removed existing database file${NC}"
    fi
    
    echo
fi

# Build Docker image
if [ $SKIP_BUILD -eq 0 ]; then
    echo -e "${YELLOW}Building Docker image...${NC}"
    
    docker build -f Dockerfile.failover -t sqlite-failover .
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Failed to build Docker image${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}Docker image built successfully!${NC}"
    echo
fi

# Run the failover scenario
echo -e "${YELLOW}Starting failover scenario...${NC}"
echo -e "${GRAY}  1. Initialize database with 100 products${NC}"
echo -e "${GRAY}  2. Instance1 will update prices for 3 loops then crash${NC}"
echo -e "${GRAY}  3. Instance2 will wait, then take over for 5 more loops${NC}"
echo

# Run with timestamps
export COMPOSE_PROJECT_NAME="sqlite-failover"
docker-compose -f docker-compose.failover.yml up --abort-on-container-exit

if [ $? -ne 0 ]; then
    echo -e "${YELLOW}Warning: Docker Compose exited with non-zero code${NC}"
fi

echo
echo -e "${YELLOW}Checking results...${NC}"

# Show logs from each instance
echo
echo -e "${CYAN}Instance1 logs (last 10 lines):${NC}"
docker-compose -f docker-compose.failover.yml logs --tail=10 instance1

echo
echo -e "${CYAN}Instance2 logs (last 10 lines):${NC}"
docker-compose -f docker-compose.failover.yml logs --tail=10 instance2

# Verify the database
DB_PATH="$DATA_DIR/failover.db"
if [ -f "$DB_PATH" ]; then
    echo
    echo -e "${GREEN}Database file exists at: $DB_PATH${NC}"
    
    # Get file size
    FILE_SIZE=$(du -k "$DB_PATH" | cut -f1)
    echo -e "${GRAY}Database size: ${FILE_SIZE} KB${NC}"
else
    echo -e "${RED}Database file not found at: $DB_PATH${NC}"
    exit 1
fi

echo
echo -e "${GREEN}Failover test completed!${NC}"
echo
echo -e "${GRAY}To clean up containers and data, run: $0 --clean${NC}"