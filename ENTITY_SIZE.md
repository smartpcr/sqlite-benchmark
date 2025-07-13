# BenchmarkEntity Size Analysis

## Entity Structure

```csharp
public class BenchmarkEntity
{
    public long Id { get; set; }              // 8 bytes
    public string Name { get; set; }          // Variable
    public int Value { get; set; }            // 4 bytes
    public string Description { get; set; }   // Variable
    public bool IsActive { get; set; }        // 1 byte
    public double Score { get; set; }         // 8 bytes
    public string Tags { get; set; }          // Variable
    public DateTime CreatedAt { get; set; }   // 8 bytes
}
```

## Field Size Breakdown

### Fixed-size fields:
- `Id` (long): 8 bytes
- `Value` (int): 4 bytes
- `IsActive` (bool): 1 byte
- `Score` (double): 8 bytes
- `CreatedAt` (DateTime): 8 bytes
- **Total fixed**: 29 bytes

### Variable-size fields (strings):

Based on the benchmark data generation:

1. **Name**: `"Entity {i}"` where i = 1 to 10000
   - For i=1: "Entity 1" = 8 characters
   - For i=100: "Entity 100" = 10 characters
   - For i=1000: "Entity 1000" = 11 characters
   - For i=10000: "Entity 10000" = 12 characters

2. **Description**: `"Description for entity {i} with some additional text to make it more realistic"`
   - Base text: 74 characters
   - Plus number length (1-5 digits)
   - Total: 75-79 characters

3. **Tags**: `"tag1,tag2,tag3,tag4,tag5"`
   - Fixed at 24 characters

## Storage Sizes

### In-Memory Size (Approximate)
- Object header overhead: ~24 bytes
- Field storage: 29 bytes (fixed fields)
- String references (3 x 8 bytes): 24 bytes
- String data (UTF-16): ~220-230 bytes
- **Total**: ~300-310 bytes per entity

### JSON Serialized Size (Stored in SQLite)

Example JSON for entity #1000:
```json
{
  "Id": 1000,
  "Name": "Entity 1000",
  "Value": 1000,
  "Description": "Description for entity 1000 with some additional text to make it more realistic",
  "IsActive": true,
  "Score": 1500.0,
  "Tags": "tag1,tag2,tag3,tag4,tag5",
  "CreatedAt": "2024-01-15T10:30:00.000Z"
}
```

Size calculation:
- JSON structure overhead: ~80 bytes (field names, quotes, brackets)
- Id: 4 bytes
- Name: 11 bytes
- Value: 4 bytes
- Description: 79 bytes
- IsActive: 4 bytes
- Score: 6 bytes
- Tags: 24 bytes
- CreatedAt: 24 bytes

**Total JSON size**: ~236 bytes for entity #1000

## Size by Entity Number

| Entity # | Name Length | JSON Size (approx) |
|----------|-------------|-------------------|
| 1        | 8 chars     | ~232 bytes        |
| 10       | 9 chars     | ~233 bytes        |
| 100      | 10 chars    | ~234 bytes        |
| 1000     | 11 chars    | ~236 bytes        |
| 10000    | 12 chars    | ~237 bytes        |

## Summary

- **In-memory size**: ~300-310 bytes per entity
- **JSON size (stored)**: ~230-240 bytes per entity
- **SQLite storage**: ~240-250 bytes per row (including SQLite overhead)

For benchmark calculations:
- 100 entities ≈ 24 KB (JSON)
- 1,000 entities ≈ 240 KB (JSON)
- 10,000 entities ≈ 2.4 MB (JSON)
- 100,000 entities ≈ 24 MB (JSON)

The actual SQLite database file will be slightly larger due to:
- B-tree structure overhead
- Index storage
- Page alignment
- Write-ahead log (WAL) file