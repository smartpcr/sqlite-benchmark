# Composite Foreign Key Example

## Overview
Composite foreign keys are used when a table references another table using multiple columns. Each participating column must have a `ForeignKeyAttribute` with the same constraint name.

## Example: CacheEntry referencing CacheEntity

### Referenced Table (CacheEntity)
```csharp
[Table("CacheEntity")]
public class CacheEntity
{
    [PrimaryKey(Order = 1)]
    [Column("TypeName", SQLiteDbType.Text, NotNull = true)]
    public string TypeName { get; set; }
    
    [PrimaryKey(Order = 2)]
    [Column("AssemblyVersion", SQLiteDbType.Text, NotNull = true)]
    public string AssemblyVersion { get; set; }
    
    // Other properties...
}
```

### Referencing Table (CacheEntry)
```csharp
[Table("CacheEntry")]
public class CacheEntry<T> : BaseEntity<string>
{
    // Each property participating in the composite FK must have the attribute
    [Column("TypeName", SQLiteDbType.Text, NotNull = true)]
    [ForeignKey("CacheEntity", "TypeName", Name = "FK_CacheEntry_CacheEntity", Ordinal = 0)]
    public string TypeName { get; set; }
    
    [Column("AssemblyVersion", SQLiteDbType.Text)]
    [ForeignKey("CacheEntity", "AssemblyVersion", Name = "FK_CacheEntry_CacheEntity", Ordinal = 1)]
    public string AssemblyVersion { get; set; }
    
    // Other properties...
}
```

## Key Points

1. **Same Constraint Name**: All properties participating in a composite foreign key must use the same `Name` value.

2. **Ordinal Position**: Use the `Ordinal` property to specify the order of columns. This should match the order in the referenced table's primary key.

3. **Referenced Columns**: Each `ForeignKeyAttribute` specifies which column it references in the target table.

4. **Consistency**: All attributes with the same constraint name must reference the same table and have the same ON DELETE/UPDATE actions.

## Generated SQL

The above configuration generates:
```sql
CONSTRAINT FK_CacheEntry_CacheEntity 
FOREIGN KEY (TypeName, AssemblyVersion) 
REFERENCES CacheEntity(TypeName, AssemblyVersion)
```