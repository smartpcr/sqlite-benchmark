# SQLite Persistence ETW Tracing

This folder contains the Event Tracing for Windows (ETW) implementation for SQLite persistence operations.

## Components

### PersistenceEventSource
- Event source with name: `Microsoft-AzureStack-Persistence`
- Implements ETW events for all persistence operations
- Singleton instance accessible via `PersistenceEventSource.Log`

### Logger
- Static wrapper around `PersistenceEventSource` for easier usage
- Provides convenient methods with automatic exception handling
- Includes helper methods for tracking operation duration

## Event Categories (Keywords)

- **Database** (0x0001): General database operations
- **Query** (0x0002): Query-specific operations
- **Transaction** (0x0004): Transaction management
- **Performance** (0x0008): Performance-related events
- **Error** (0x0010): Error events
- **Batch** (0x0020): Batch operations
- **Bulk** (0x0040): Bulk import/export operations
- **Cache** (0x0080): Cache-related events

## Event Tasks

1. **Create**: Entity creation operations
2. **Read**: Entity retrieval operations
3. **Update**: Entity update operations
4. **Delete**: Entity deletion operations
5. **Query**: Query operations
6. **Transaction**: Transaction operations
7. **Batch**: Batch operations
8. **Bulk**: Bulk operations
9. **Maintenance**: Maintenance operations (optimization, cleanup)

## Usage Examples

### Basic Operation Logging

```csharp
using SQLite.Lib.Traces;
using System.Diagnostics;

// Simple logging
Logger.CreateStart(entity.Id, "CacheEntry");
// ... perform operation ...
Logger.CreateStop(entity.Id, "CacheEntry", stopwatch);

// Error logging
try
{
    // ... operation code ...
}
catch (Exception ex)
{
    Logger.CreateFailed(entity.Id, "CacheEntry", ex);
    throw;
}
```

### Using Helper Methods

```csharp
// Synchronous operation tracking
var result = Logger.TrackOperation(
    "CreateEntity",
    () => CreateEntityInternal(entity),
    () => Logger.CreateStart(entity.Id, tableName),
    (sw) => Logger.CreateStop(entity.Id, tableName, sw),
    (ex) => Logger.CreateFailed(entity.Id, tableName, ex)
);

// Asynchronous operation tracking
var result = await Logger.TrackOperationAsync(
    "GetEntity",
    async () => await GetEntityInternalAsync(key),
    () => Logger.GetStart(key.ToString(), tableName),
    (sw) => Logger.GetStop(key.ToString(), tableName, sw),
    (ex) => Logger.GetFailed(key.ToString(), tableName, ex)
);
```

### Batch Operations

```csharp
Logger.BatchOperationStart("CreateBatch", entities.Count(), listCacheKey);
try
{
    // ... batch operation ...
    Logger.BatchOperationStop("CreateBatch", entities.Count(), listCacheKey, stopwatch);
}
catch (Exception ex)
{
    Logger.BatchOperationFailed("CreateBatch", listCacheKey, ex);
    throw;
}
```

### Query Operations

```csharp
Logger.QueryStart(tableName);
try
{
    var results = await ExecuteQueryAsync(predicate);
    Logger.QueryStop(tableName, results.Count(), stopwatch);
    return results;
}
catch (Exception ex)
{
    Logger.QueryFailed(tableName, ex);
    throw;
}
```

## Collecting ETW Traces

### Using PerfView

```powershell
# Start collection
PerfView /OnlyProviders=*Microsoft-AzureStack-Persistence collect

# With specific keywords (e.g., Database and Error)
PerfView /OnlyProviders=*Microsoft-AzureStack-Persistence:0x11 collect
```

### Using WPA (Windows Performance Analyzer)

1. Create a custom profile with provider GUID
2. Enable the `Microsoft-AzureStack-Persistence` provider
3. Collect and analyze traces

### Using logman

```powershell
# Create trace session
logman create trace SQLitePersistence -p "Microsoft-AzureStack-Persistence" 0xFFFFFFFF 0xFF -o trace.etl

# Start trace
logman start SQLitePersistence

# Stop trace
logman stop SQLitePersistence

# Delete trace session
logman delete SQLitePersistence
```

## Performance Considerations

- ETW is designed to be low-overhead when no listeners are attached
- Verbose events (like SQL execution) are at Verbose level and disabled by default
- Use keywords to filter events and reduce overhead
- The EventSource is optimized for minimal allocation

## Best Practices

1. Always use the `Logger` class instead of directly accessing `PersistenceEventSource`
2. Use appropriate event levels (Verbose, Informational, Warning, Error)
3. Include meaningful context in event messages
4. Use stopwatch for accurate timing measurements
5. Log both start and stop events for operations to enable duration analysis
6. Use the helper methods (`TrackOperation`, `TrackOperationAsync`) for consistent logging