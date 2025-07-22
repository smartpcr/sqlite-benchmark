# Appendix A: SQLite Cache Schema

## A.1 Core Tables

### Version Table
```sql
-- Global version sequence table
CREATE TABLE IF NOT EXISTS Version (
    Version INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL DEFAULT (datetime('now'))
);
```

### CacheEntry Table
```sql
CREATE TABLE IF NOT EXISTS CacheEntry (
    CacheKey TEXT NOT NULL,
    Version INTEGER NOT NULL,
    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
    LastWriteTime TEXT NOT NULL DEFAULT (datetime('now')),
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Data BLOB NOT NULL,
    TypeName TEXT NOT NULL,
    AssemblyVersion TEXT NOT NULL,
    Size INTEGER NOT NULL,
    AbsoluteExpiration TEXT,
    SlidingExpirationSeconds INTEGER,
    Tags TEXT,
    PRIMARY KEY (CacheKey, Version),
    CONSTRAINT FK_CacheEntry_Version FOREIGN KEY (Version) REFERENCES Version(Version) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_CacheEntry_CacheEntity FOREIGN KEY (TypeName, AssemblyVersion) REFERENCES CacheEntity(TypeName, AssemblyVersion) ON DELETE NO ACTION ON UPDATE NO ACTION
);
```

## A.2 Audit Tables

### CacheUpdateHistory Table
```sql
CREATE TABLE IF NOT EXISTS CacheUpdateHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CacheKey TEXT NOT NULL,
    TypeName TEXT NOT NULL,
    Operation TEXT NOT NULL CHECK (Operation IN ('INSERT', 'UPDATE', 'DELETE')),
    Version INTEGER NOT NULL,
    OldVersion INTEGER NULL, -- For UPDATE operations, stores the previous version
    Size INTEGER NOT NULL,
    CallerFilePath TEXT NULL,
    CallerMemberName TEXT NULL,
    CallerLineNumber INTEGER NULL,
    UpdateTime TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (Version) REFERENCES Version(Version)
);
```

### CacheAccessHistory Table
```sql
CREATE TABLE IF NOT EXISTS CacheAccessHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CacheKey TEXT NOT NULL,
    TypeName TEXT NOT NULL,
    Operation TEXT NOT NULL CHECK (Operation IN ('GET', 'EXISTS')),
    CacheHit INTEGER NOT NULL CHECK (CacheHit IN (0, 1)),
    Version INTEGER NULL, -- Version that was accessed
    CallerFile TEXT NULL,
    CallerMember TEXT NULL,
    CallerLineNumber INTEGER NULL,
    ResponseTimeMs INTEGER NULL,
    Timestamp TEXT NOT NULL DEFAULT (datetime('now'))
);
```

## A.3 Configuration and Metadata Tables

### CacheEntity Table
```sql
CREATE TABLE IF NOT EXISTS CacheEntity (
    TypeName TEXT NOT NULL,
    AssemblyVersion TEXT NOT NULL,
    StoreType TEXT NOT NULL,
    SerializerType TEXT NOT NULL DEFAULT 'JSON',
    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
    LastModifiedTime TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (TypeName, AssemblyVersion)
);
```

### UpdateEntity Table
```sql
CREATE TABLE IF NOT EXISTS UpdateEntity (
    CacheKey TEXT NOT NULL,
    Version INTEGER NOT NULL,
    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
    LastWriteTime TEXT NOT NULL DEFAULT (datetime('now')),
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    DisplayName TEXT NOT NULL,
    UpdateVersion TEXT NOT NULL,
    SbeVersion TEXT,
    Description TEXT,
    State TEXT NOT NULL,
    Publisher TEXT,
    InstalledDate TEXT,
    KbLink TEXT,
    ReleaseLink TEXT,
    MinVersionRequired TEXT,
    MinSbeVersionRequired TEXT,
    PackagePath TEXT,
    PackageSizeInMb INTEGER NOT NULL,
    PackageType TEXT,
    DeliveryType TEXT,
    RebootRequired TEXT,
    InstallType TEXT,
    AvailabilityType TEXT,
    HealthState TEXT,
    HealthCheckDate TEXT,
    IsRecalled INTEGER NOT NULL DEFAULT 0,
    OemFamily TEXT,
    Prerequisites TEXT,
    AdditionalProperties TEXT,
    BillOfMaterials TEXT,
    HealthCheckResult TEXT,
    UpdateStateProperties TEXT,
    DeferScanAndDownload INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (CacheKey, Version),
    CONSTRAINT FK_UpdateEntity_Version FOREIGN KEY (Version) REFERENCES Version(Version) ON DELETE NO ACTION ON UPDATE NO ACTION
);
```

### UpdateRunEntity Table
```sql
CREATE TABLE IF NOT EXISTS UpdateRunEntity (
    CacheKey TEXT NOT NULL,
    Version INTEGER NOT NULL,
    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
    LastWriteTime TEXT NOT NULL DEFAULT (datetime('now')),
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Name TEXT NOT NULL,
    ResourceId TEXT NOT NULL,
    ParentId TEXT,
    ResourceType TEXT NOT NULL,
    TimeStarted TEXT,
    LastUpdatedTime TEXT,
    Duration INTEGER NOT NULL,
    State TEXT NOT NULL,
    OnCompleteActionSuccess INTEGER NOT NULL DEFAULT 0,
    PreparationDownloadPercentage INTEGER NOT NULL DEFAULT 0,
    IsPreparationRun INTEGER NOT NULL DEFAULT 0,
    Progress TEXT,
    ProgressStatus TEXT,
    ProgressDescription TEXT,
    CurrentStepName TEXT,
    TotalSteps INTEGER NOT NULL DEFAULT 0,
    CompletedSteps INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT,
    UpdateName TEXT,
    UpdateVersion TEXT,
    ActionPlanId TEXT,
    ActionPlanInstanceId TEXT,
    PRIMARY KEY (CacheKey, Version),
    CONSTRAINT FK_UpdateRunEntity_Version FOREIGN KEY (Version) REFERENCES Version(Version) ON DELETE NO ACTION ON UPDATE NO ACTION
);
```

### CacheStatistics Table
```sql
CREATE TABLE IF NOT EXISTS CacheStatistics (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TypeName TEXT NOT NULL,
    StatDate TEXT NOT NULL,
    HitCount INTEGER NOT NULL DEFAULT 0,
    MissCount INTEGER NOT NULL DEFAULT 0,
    EvictionCount INTEGER NOT NULL DEFAULT 0,
    UpdateCount INTEGER NOT NULL DEFAULT 0,
    DeleteCount INTEGER NOT NULL DEFAULT 0,
    AverageLoadTimeMs REAL NULL,
    AverageItemSize REAL NULL,
    TotalItemCount INTEGER NOT NULL DEFAULT 0,
    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
    LastUpdatedTime TEXT NOT NULL DEFAULT (datetime('now'))
);
```

### CacheMetadata Table
```sql
CREATE TABLE IF NOT EXISTS CacheMetadata (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CacheKey TEXT NOT NULL,
    Version INTEGER NOT NULL,
    MetadataType TEXT NOT NULL,
    MetadataKey TEXT NOT NULL,
    MetadataValue TEXT NOT NULL,
    ExtractedTime TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (CacheKey, Version) REFERENCES CacheEntry(CacheKey, Version)
);
```

## A.4 Performance Indexes

```sql
CREATE INDEX IF NOT EXISTS IX_CacheEntry_Key ON CacheEntry(CacheKey);
CREATE INDEX IF NOT EXISTS IX_CacheEntry_LastWriteTime ON CacheEntry(LastWriteTime);
CREATE INDEX IF NOT EXISTS IX_CacheEntry_Version ON CacheEntry(Version);
CREATE INDEX IF NOT EXISTS IX_CacheEntry_Type ON CacheEntry(TypeName);
CREATE INDEX IF NOT EXISTS IX_CacheEntry_AbsoluteExpiration ON CacheEntry(AbsoluteExpiration);
CREATE INDEX IF NOT EXISTS IX_CacheEntry_Tags ON CacheEntry(Tags);
CREATE INDEX IF NOT EXISTS IX_CacheUpdateHistory_CacheKey ON CacheUpdateHistory(CacheKey);
CREATE INDEX IF NOT EXISTS IX_CacheUpdateHistory_Version ON CacheUpdateHistory(Version);
CREATE INDEX IF NOT EXISTS IX_CacheAccessHistory_CacheKey ON CacheAccessHistory(CacheKey);
CREATE INDEX IF NOT EXISTS IX_CacheStatistics_TypeName_StatDate ON CacheStatistics(TypeName, StatDate);
CREATE INDEX IF NOT EXISTS IX_UpdateEntity_Key ON UpdateEntity(CacheKey);
CREATE INDEX IF NOT EXISTS IX_UpdateEntity_LastWriteTime ON UpdateEntity(LastWriteTime);
CREATE INDEX IF NOT EXISTS IX_UpdateEntity_Version ON UpdateEntity(Version);
CREATE INDEX IF NOT EXISTS IX_UpdateEntity_UpdateVersion ON UpdateEntity(UpdateVersion);
CREATE INDEX IF NOT EXISTS IX_UpdateEntity_State ON UpdateEntity(State);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_Key ON UpdateRunEntity(CacheKey);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_LastWriteTime ON UpdateRunEntity(LastWriteTime);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_Version ON UpdateRunEntity(Version);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_Name ON UpdateRunEntity(Name);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_TimeStarted ON UpdateRunEntity(TimeStarted);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_State ON UpdateRunEntity(State);
CREATE INDEX IF NOT EXISTS IX_UpdateRunEntity_UpdateName ON UpdateRunEntity(UpdateName);
```

## A.5 CacheEntry<T> Storage Details

The `CacheEntry` table is designed to store generic `CacheEntry<T>` objects where T can be any class type. Here's how the mapping works:

### Foreign Key Relationships

The CacheEntry table has two foreign key constraints:
1. **Version FK**: References the Version table to ensure version integrity
2. **Composite FK to CacheEntity**: (TypeName, AssemblyVersion) references CacheEntity(TypeName, AssemblyVersion) to ensure type registration

### Storage Pattern

```sql
-- Example: Storing a CacheEntry<UpdateEntity> object
INSERT INTO CacheEntry (
    CacheKey,           -- e.g., "update-123"
    Version,            -- from global Version sequence
    TypeName,           -- "UpdateEntity" (the T in CacheEntry<T>)
    AssemblyVersion,    -- e.g., "1.0.0.0"
    Data,               -- BLOB containing serialized CacheEntry<UpdateEntity>
    AbsoluteExpiration, -- Unix timestamp or NULL
    Size,               -- byte size of serialized data
    IsDeleted,          -- 0 (false)
    CreatedTime,        -- current timestamp
    LastWriteTime       -- current timestamp
) VALUES (
    @cacheKey,
    @version,
    @typeName,
    @assemblyVersion,
    @data,              -- Serialized byte array of entire CacheEntry<T> object
    @absoluteExpiration,
    @size,
    0,
    datetime('now'),
    datetime('now')
);
```

### Serialization Structure

The `Data` BLOB column contains the serialized `CacheEntry<T>` object which includes:
- The cache key
- The wrapped value of type T
- Type information (TypeName, AssemblyVersion)
- Expiration settings (AbsoluteExpiration, SlidingExpiration)
- Metadata tags
- Tracking fields (CreatedTime, LastWriteTime, Version, etc.)

### Type Resolution

When retrieving from the cache:
1. Query by CacheKey to get the latest version
2. Check TypeName to verify expected type
3. Deserialize the Data BLOB back to `CacheEntry<T>`
4. Access the strongly-typed Value property

### Example Query Pattern

```sql
-- Get latest version of a cached UpdateEntity
SELECT * FROM CacheEntry
WHERE CacheKey = 'update-123'
  AND TypeName = 'UpdateEntity'
ORDER BY Version DESC
LIMIT 1;

-- Note: Do NOT filter by IsDeleted = 0 in the query!
-- The query must return the latest version regardless of deletion status.
-- The application code should check the IsDeleted flag after retrieval
-- to determine if the entry exists or has been deleted.
```