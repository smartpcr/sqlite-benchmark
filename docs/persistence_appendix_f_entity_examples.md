# Appendix F: Entity Examples with Attribute-Based Mapping

## UpdateEntity Example

```csharp
[Table("UpdateEntity")]
public class UpdateEntity : IEntity<string>
{
    [PrimaryKey]
    [Column("UpdateId", SqlDbType.NVarChar, Size = 200)]
    public string Key { get; set; }
    
    [Column("UpdateType", SqlDbType.NVarChar, Size = 50)]
    [Index("IX_UpdateEntity_Type")]
    public string Type { get; set; }
    
    [Column("State", SqlDbType.NVarChar, Size = 50)]
    [Index("IX_UpdateEntity_State")]
    public string State { get; set; }
    
    [Column("Priority", SqlDbType.Int)]
    public int Priority { get; set; }
    
    [Column("Payload", SqlDbType.NVarChar)]
    [JsonConverter(typeof(JsonStringSerializer))]
    public string SerializedPayload { get; set; }
    
    [Column("Size", SqlDbType.BigInt)]
    [Computed]
    public long Size => Encoding.UTF8.GetByteCount(this.SerializedPayload ?? string.Empty);
    
    [Column("DependsOn", SqlDbType.NVarChar, Size = 4000)]
    public string DependsOn { get; set; }
    
    [Column("ScheduledTime", SqlDbType.DateTime2)]
    [Index("IX_UpdateEntity_ScheduledTime")]
    public DateTime? ScheduledTime { get; set; }
    
    [Column("ExpirationTime", SqlDbType.DateTime2)]
    public DateTime? ExpirationTime { get; set; }
    
    // Bookkeeping fields
    [AuditField(AuditFieldType.CreatedTime)]
    [Column("CreatedTime", SqlDbType.DateTime2)]
    public DateTime CreatedTime { get; set; }
    
    [AuditField(AuditFieldType.LastWriteTime)]
    [Column("LastWriteTime", SqlDbType.DateTime2)]
    public DateTime LastWriteTime { get; set; }
    
    [AuditField(AuditFieldType.Version)]
    [Column("Version", SqlDbType.BigInt)]
    public long Version { get; set; }
    
    [AuditField(AuditFieldType.IsDeleted)]
    [Column("IsDeleted", SqlDbType.Bit)]
    public bool IsDeleted { get; set; }
    
    // Not mapped to database
    [NotMapped]
    public UpdatePayload Payload { get; set; }
}
```

## CacheEntry Example

> **Note**: This shows a non-generic CacheEntry for direct table mapping. For type-safe caching operations, use the generic `CacheEntry<T>` wrapper documented in [Appendix C](persistence_appendix_c_core_contracts.md#c3-cache-entry-wrapper).

```csharp
[Table("CacheEntry")]
public class CacheEntry : IEntity<string>
{
    [PrimaryKey]
    [Column("CacheKey", SqlDbType.NVarChar, Size = 500)]
    public string Key { get; set; }
    
    [Column("EntityType", SqlDbType.NVarChar, Size = 200)]
    [Index("IX_CacheEntry_Type")]
    public string EntityType { get; set; }
    
    [Column("Value", SqlDbType.NVarChar)]
    public string SerializedValue { get; set; }
    
    [Column("Size", SqlDbType.BigInt)]
    [Computed]
    public long Size => Encoding.UTF8.GetByteCount(this.SerializedValue ?? string.Empty);
    
    [Column("TTL", SqlDbType.Int)]
    public int? TTLSeconds { get; set; }
    
    [Column("Tags", SqlDbType.NVarChar, Size = 2000)]
    public string Tags { get; set; }
    
    [Column("ParentKey", SqlDbType.NVarChar, Size = 500)]
    [ForeignKey("CacheEntry", "CacheKey")]
    [Index("IX_CacheEntry_Parent")]
    public string ParentKey { get; set; }
    
    // Bookkeeping fields
    [AuditField(AuditFieldType.CreatedTime)]
    [Column("CreatedTime", SqlDbType.DateTime2)]
    public DateTime CreatedTime { get; set; }
    
    [AuditField(AuditFieldType.LastWriteTime)]
    [Column("LastWriteTime", SqlDbType.DateTime2)]
    public DateTime LastWriteTime { get; set; }
    
    [AuditField(AuditFieldType.Version)]
    [Column("Version", SqlDbType.BigInt)]
    public long Version { get; set; }
    
    [AuditField(AuditFieldType.IsDeleted)]
    [Column("IsDeleted", SqlDbType.Bit)]
    public bool IsDeleted { get; set; }
}
```

## Usage Example with BaseEntityMapper

```csharp
// Create attribute-based mapper for automatic schema generation
var mapper = new BaseEntityMapper<UpdateEntity>();

// Generate DDL from attributes
string createTableSql = mapper.GenerateCreateTableSql();
IEnumerable<string> createIndexSql = mapper.GenerateCreateIndexSql();

// Execute DDL to create schema
using (var connection = new SQLiteConnection(connectionString))
{
    await connection.OpenAsync();
    
    // Create table
    using (var cmd = new SQLiteCommand(createTableSql, connection))
    {
        await cmd.ExecuteNonQueryAsync();
    }
    
    // Create indexes
    foreach (var indexSql in createIndexSql)
    {
        using (var cmd = new SQLiteCommand(indexSql, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

// Create persistence provider with the mapper
var serializer = SerializerResolver.GetSerializer<UpdateEntity>();
var provider = new SqlitePersistenceProvider<UpdateEntity>(
    connectionString, 
    mapper, 
    serializer
);

// Use the provider normally
var entity = new UpdateEntity
{
    Key = "update-123",
    Type = "OEM",
    State = "Ready",
    Priority = 1,
    Payload = new UpdatePayload { /* ... */ }
};

// Serialize payload before saving
entity.SerializedPayload = await serializer.SerializeAsync(entity.Payload);

// Create/Update operations handle bookkeeping fields automatically
await provider.CreateAsync(entity);

// Query with LINQ expressions
var pendingUpdates = await provider.QueryAsync(
    e => e.State == "Pending" && e.Priority > 0,
    orderBy: e => e.Priority,
    descending: true
);
```

## Custom Entity with Composite Key

```csharp
[Table("NodeUpdate")]
public class NodeUpdate : IEntity<string>
{
    [Column("NodeId", SqlDbType.NVarChar, Size = 100)]
    [Index("IX_NodeUpdate_Composite", Order = 1)]
    public string NodeId { get; set; }
    
    [Column("UpdateId", SqlDbType.NVarChar, Size = 200)]
    [Index("IX_NodeUpdate_Composite", Order = 2)]
    public string UpdateId { get; set; }
    
    // Composite key for IEntity
    [PrimaryKey]
    [Column("CompositeKey", SqlDbType.NVarChar, Size = 300)]
    public string Key => $"{this.NodeId}:{this.UpdateId}";
    
    [Column("Status", SqlDbType.NVarChar, Size = 50)]
    public string Status { get; set; }
    
    [Column("Progress", SqlDbType.Int)]
    public int ProgressPercentage { get; set; }
    
    [Column("StartTime", SqlDbType.DateTime2)]
    public DateTime? StartTime { get; set; }
    
    [Column("EndTime", SqlDbType.DateTime2)]
    public DateTime? EndTime { get; set; }
    
    [Column("ErrorMessage", SqlDbType.NVarChar, Size = 4000)]
    public string ErrorMessage { get; set; }
    
    // Standard bookkeeping fields
    [AuditField(AuditFieldType.CreatedTime)]
    [Column("CreatedTime", SqlDbType.DateTime2)]
    public DateTime CreatedTime { get; set; }
    
    [AuditField(AuditFieldType.LastWriteTime)]
    [Column("LastWriteTime", SqlDbType.DateTime2)]
    public DateTime LastWriteTime { get; set; }
    
    [AuditField(AuditFieldType.Version)]
    [Column("Version", SqlDbType.BigInt)]
    public long Version { get; set; }
    
    [AuditField(AuditFieldType.IsDeleted)]
    [Column("IsDeleted", SqlDbType.Bit)]
    public bool IsDeleted { get; set; }
}
```

## Key Features of Attribute-Based Mapping

1. **Automatic DDL Generation**: The BaseEntityMapper generates CREATE TABLE statements from attributes
2. **Type Safety**: SQL types are validated at compile time via SqlDbType enum
3. **Index Management**: Composite and single-column indexes are defined declaratively
4. **Computed Columns**: Properties can be marked as computed for database-generated values
5. **Audit Fields**: Standard bookkeeping fields are handled automatically
6. **Foreign Keys**: Relationships can be declared for referential integrity
7. **Custom Serialization**: JsonConverter attributes control serialization behavior