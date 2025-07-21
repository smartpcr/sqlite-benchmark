# Appendix C: Core Persistence Contracts

## C.1 Entity Contracts

### IEntity Interface

```csharp
using System;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Defines the contract for persistable entities with strongly-typed keys.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        [JsonProperty("CacheKey")]
        TKey Id { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was created.
        /// </summary>
        DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was last modified.
        /// </summary>
        DateTimeOffset LastWriteTime { get; set; }

        /// <summary>
        /// Gets or sets the version number for optimistic concurrency control.
        /// </summary>
        long Version { get; set; }

        /// <summary>
        /// Gets or sets whether the entity has been soft deleted.
        /// </summary>
        bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the expiration time for the entity (null for no expiration).
        /// </summary>
        DateTimeOffset? ExpirationTime { get; set; }
    }
}
```

## C.2 Caller Information

### CallerInfo Class

```csharp
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Provides information about the caller of a persistence operation.
    /// </summary>
    public class CallerInfo
    {
        /// <summary>
        /// Gets or sets the calling method name (automatically populated).
        /// </summary>
        public string CallerMemberName { get; set; }

        /// <summary>
        /// Gets or sets the source file path (automatically populated).
        /// </summary>
        public string CallerFilePath { get; set; }

        /// <summary>
        /// Gets or sets the line number in the source file (automatically populated).
        /// </summary>
        public int CallerLineNumber { get; set; }

        /// <summary>
        /// Creates a new CallerInfo instance with automatic caller information.
        /// </summary>
        public static CallerInfo Create(
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            return new CallerInfo
            {
                CallerMemberName = memberName,
                CallerFilePath = filePath,
                CallerLineNumber = lineNumber
            };
        }
    }
}

/// <summary>
/// Exception thrown when an optimistic concurrency conflict occurs.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when attempting to create an entity that already exists.
/// </summary>
public class EntityAlreadyExistsException : InvalidOperationException
{
    public string EntityKey { get; }

    public EntityAlreadyExistsException(string entityKey, string message) : base(message)
    {
        this.EntityKey = entityKey;
    }
}
```

## C.3 Cache Entry Wrapper

### CacheEntry<T> Generic Wrapper

```csharp
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Cache
{
    /// <summary>
    /// Generic wrapper for cache entries that provides type-safe storage and retrieval
    /// with built-in metadata tracking and serialization support.
    /// </summary>
    /// <typeparam name="T">The type of the cached value</typeparam>
    [DataContract]
    public class CacheEntry<T> : IEntity<string>
    {
        /// <summary>
        /// Gets or sets the cache key (primary key).
        /// </summary>
        [DataMember]
        [JsonProperty("cacheKey")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the cached value.
        /// </summary>
        [DataMember]
        [JsonProperty("value")]
        public T Value { get; set; }

        /// <summary>
        /// Gets or sets the type name of the cached value for deserialization.
        /// </summary>
        [DataMember]
        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the assembly-qualified type name for version-safe deserialization.
        /// </summary>
        [DataMember]
        [JsonProperty("assemblyQualifiedName")]
        public string AssemblyQualifiedName { get; set; }

        /// <summary>
        /// Gets or sets the size of the serialized value in bytes.
        /// </summary>
        [DataMember]
        [JsonProperty("size")]
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the absolute expiration time for this cache entry.
        /// </summary>
        [DataMember]
        [JsonProperty("absoluteExpiration")]
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets the sliding expiration window for this cache entry.
        /// </summary>
        [DataMember]
        [JsonProperty("slidingExpiration")]
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets tags associated with this cache entry for metadata queries.
        /// </summary>
        [DataMember]
        [JsonProperty("tags")]
        public string[] Tags { get; set; }

        // IEntity<string> implementation
        [DataMember]
        [JsonProperty("createdTime")]
        public DateTimeOffset CreatedTime { get; set; }

        [DataMember]
        [JsonProperty("lastWriteTime")]
        public DateTimeOffset LastWriteTime { get; set; }

        [DataMember]
        [JsonProperty("version")]
        public long Version { get; set; }

        [DataMember]
        [JsonProperty("isDeleted")]
        public bool IsDeleted { get; set; }

        [DataMember]
        [JsonProperty("expirationTime")]
        public DateTimeOffset? ExpirationTime { get; set; }

        /// <summary>
        /// Creates a new CacheEntry<T> with the specified value and options.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="options">Cache entry options</param>
        /// <returns>A new CacheEntry<T> instance</returns>
        public static CacheEntry<T> Create(string key, T value, CacheEntryOptions options = null)
        {
            var entry = new CacheEntry<T>
            {
                Id = key,
                Value = value,
                TypeName = typeof(T).Name,
                AssemblyQualifiedName = typeof(T).AssemblyQualifiedName,
                CreatedTime = DateTimeOffset.UtcNow,
                LastWriteTime = DateTimeOffset.UtcNow,
                Version = 1,
                IsDeleted = false
            };

            if (options != null)
            {
                entry.AbsoluteExpiration = options.AbsoluteExpiration;
                entry.SlidingExpiration = options.SlidingExpiration;
                entry.Tags = options.Tags;
                entry.ExpirationTime = options.AbsoluteExpiration;
            }

            return entry;
        }

        /// <summary>
        /// Updates the value and metadata of this cache entry.
        /// </summary>
        /// <param name="newValue">The new value</param>
        /// <param name="incrementVersion">Whether to increment the version number</param>
        public void UpdateValue(T newValue, bool incrementVersion = true)
        {
            this.Value = newValue;
            this.LastWriteTime = DateTimeOffset.UtcNow;
            
            if (incrementVersion)
            {
                this.Version++;
            }
        }

        /// <summary>
        /// Checks if this cache entry has expired based on its expiration settings.
        /// </summary>
        /// <returns>True if expired; otherwise false</returns>
        public bool IsExpired()
        {
            var now = DateTimeOffset.UtcNow;

            // Check absolute expiration
            if (this.AbsoluteExpiration.HasValue && now > this.AbsoluteExpiration.Value)
            {
                return true;
            }

            // Check sliding expiration
            if (this.SlidingExpiration.HasValue)
            {
                var slidingExpirationTime = this.LastWriteTime.Add(this.SlidingExpiration.Value);
                if (now > slidingExpirationTime)
                {
                    return true;
                }
            }

            // Check IEntity expiration time
            if (this.ExpirationTime.HasValue && now > this.ExpirationTime.Value)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Refreshes the sliding expiration window by updating the last write time.
        /// </summary>
        public void RefreshExpiration()
        {
            if (this.SlidingExpiration.HasValue)
            {
                this.LastWriteTime = DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    /// Options for creating cache entries.
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Gets or sets an absolute expiration date for the cache entry.
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets how long a cache entry can be inactive before it will be removed.
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with this cache entry.
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Gets or sets the priority for cache eviction (future use).
        /// </summary>
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
    }

    /// <summary>
    /// Cache item priority for eviction policies.
    /// </summary>
    public enum CacheItemPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        NeverRemove = 3
    }
}
```

### CacheEntry<T> Usage Examples

```csharp
// Example 1: Creating a cache entry for an Update entity
var update = new UpdateEntity { Key = "update-123", Type = "OEM" };
var cacheEntry = CacheEntry<UpdateEntity>.Create(
    "update-123",
    update,
    new CacheEntryOptions
    {
        AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),
        Tags = new[] { "OEM", "Critical", "Version:2.1.2403.0" }
    }
);

// Example 2: Storing in persistence provider
var provider = new SQLitePersistenceProvider<CacheEntry<UpdateEntity>, string>(
    connectionString,
    new CacheEntryMapper<UpdateEntity>()
);
await provider.CreateAsync(cacheEntry, callerInfo);

// Example 3: Checking expiration and refreshing
var cachedEntry = await provider.GetAsync("update-123", callerInfo);
if (cachedEntry != null && !cachedEntry.IsExpired())
{
    // Use the cached value
    var updateEntity = cachedEntry.Value;
    
    // Refresh sliding expiration if configured
    cachedEntry.RefreshExpiration();
    await provider.UpdateAsync(cachedEntry, callerInfo);
}

// Example 4: Updating cached value with version increment
cachedEntry.UpdateValue(newUpdateEntity);
await provider.UpdateAsync(cachedEntry, callerInfo);
```

### CacheEntry<T> Mapper Implementation

```csharp
/// <summary>
/// SQLite mapper for CacheEntry<T> that handles generic type serialization.
/// </summary>
public class CacheEntryMapper<T> : BaseEntityMapper<CacheEntry<T>>
{
    private readonly ISerializer<T> valueSerializer;

    public CacheEntryMapper(ISerializer<T> valueSerializer = null)
    {
        this.valueSerializer = valueSerializer ?? SerializerResolver.CreateSerializer<T>();
    }

    public override void AddParameters(SQLiteCommand command, CacheEntry<T> entity)
    {
        base.AddParameters(command, entity);
        
        // Serialize the generic value to bytes
        var serializedValue = this.valueSerializer.Serialize(entity.Value);
        command.Parameters.AddWithValue("@SerializedValue", serializedValue);
        command.Parameters.AddWithValue("@Size", serializedValue.Length);
    }

    public override CacheEntry<T> MapFromReader(IDataReader reader)
    {
        var entity = base.MapFromReader(reader);
        
        // Deserialize the generic value from bytes
        var serializedValue = (byte[])reader["SerializedValue"];
        entity.Value = this.valueSerializer.Deserialize(serializedValue);
        
        return entity;
    }
}
```

## C.4 Serialization Contracts

### ISerializer Interface

```csharp
using System;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization
{
    /// <summary>
    /// Defines the contract for entity serialization.
    /// </summary>
    /// <typeparam name="T">The entity type to serialize</typeparam>
    public interface ISerializer<T>
    {
        /// <summary>
        /// Serializes an entity to a byte array.
        /// </summary>
        /// <param name="entity">The entity to serialize</param>
        /// <returns>Serialized byte array</returns>
        byte[] Serialize(T entity);

        /// <summary>
        /// Deserializes an entity from a byte array.
        /// </summary>
        /// <param name="data">The byte array to deserialize</param>
        /// <returns>Deserialized entity</returns>
        T Deserialize(byte[] data);

        /// <summary>
        /// Gets the serializer type name for metadata tracking.
        /// </summary>
        string SerializerType { get; }
    }

    /// <summary>
    /// Resolves the appropriate serializer for an entity type based on attributes.
    /// </summary>
    public static class SerializerResolver
    {
        /// <summary>
        /// Creates a serializer instance for the specified type.
        /// Checks for [JsonConverter] attribute to determine custom serialization.
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <returns>Appropriate serializer instance</returns>
        public static ISerializer<T> CreateSerializer<T>()
        {
            var type = typeof(T);
            
            // Check for JsonConverter attribute
            var jsonConverterAttr = type.GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute>();
            if (jsonConverterAttr != null)
            {
                // Use custom converter if specified
                return new JsonSerializer<T>(jsonConverterAttr.ConverterType);
            }
            
            // Check for DataContract attribute
            var dataContractAttr = type.GetCustomAttribute<System.Runtime.Serialization.DataContractAttribute>();
            if (dataContractAttr != null)
            {
                return new DataContractSerializer<T>();
            }
            
            // Default to JSON serialization
            return new JsonSerializer<T>();
        }
    }

    /// <summary>
    /// JSON-based serializer implementation.
    /// </summary>
    public class JsonSerializer<T> : ISerializer<T>
    {
        private readonly System.Text.Json.JsonSerializerOptions options;
        private readonly Type converterType;

        public JsonSerializer(Type converterType = null)
        {
            this.converterType = converterType;
            this.options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            if (converterType != null)
            {
                var converter = Activator.CreateInstance(converterType) as System.Text.Json.Serialization.JsonConverter;
                if (converter != null)
                {
                    this.options.Converters.Add(converter);
                }
            }
        }

        public string SerializerType => this.converterType != null 
            ? $"JSON:{this.converterType.Name}" 
            : "JSON";

        public byte[] Serialize(T entity)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entity, this.options);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, this.options);
        }
    }

    /// <summary>
    /// DataContract-based serializer implementation.
    /// </summary>
    public class DataContractSerializer<T> : ISerializer<T>
    {
        private readonly System.Runtime.Serialization.DataContractSerializer serializer;

        public DataContractSerializer()
        {
            this.serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(T));
        }

        public string SerializerType => "DataContract";

        public byte[] Serialize(T entity)
        {
            using var stream = new System.IO.MemoryStream();
            this.serializer.WriteObject(stream, entity);
            return stream.ToArray();
        }

        public T Deserialize(byte[] data)
        {
            using var stream = new System.IO.MemoryStream(data);
            return (T)this.serializer.ReadObject(stream);
        }
    }
}
```

### IPersistenceProvider Interface

```csharp
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AzureStack.Services.Update.Common.Persistence.Serialization;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Defines the contract for persistence providers that handle entity storage and retrieval.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IPersistenceProvider<T, TKey> 
        where T : class, IEntity<TKey> 
        where TKey : IEquatable<TKey>
    {
        #region CRUD Operations

        /// <summary>
        /// Creates a new entity in the persistence store.
        /// </summary>
        /// <param name="entity">The entity to create</param>
        /// <param name="callerInfo">Information about the caller</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entity with updated tracking fields</returns>
        Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an entity by its primary key.
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The entity if found; otherwise null</returns>
        Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity with optimistic concurrency control.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="callerInfo">Information about the caller</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entity with incremented version</returns>
        /// <exception cref="ConcurrencyException">Thrown when version conflict detected</exception>
        Task<T> UpdateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity by its primary key (soft delete by default).
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller</param>
        /// <param name="hardDelete">If true, permanently removes the entity</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted; false if not found</returns>
        Task<bool> DeleteAsync(TKey key, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default);

        #endregion

        #region Batch Operations

        /// <summary>
        /// Creates multiple entities in a single batch operation.
        /// </summary>
        /// <param name="entities">The entities to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entities with updated tracking fields</returns>
        Task<IEnumerable<T>> CreateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves multiple entities by their primary keys.
        /// </summary>
        /// <param name="keys">The primary keys</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The found entities</returns>
        Task<IEnumerable<T>> GetBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities in a single batch operation.
        /// </summary>
        /// <param name="entities">The entities to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entities</returns>
        Task<IEnumerable<T>> UpdateBatchAsync(IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple entities by their primary keys.
        /// </summary>
        /// <param name="keys">The primary keys</param>
        /// <param name="hardDelete">If true, permanently removes the entities</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of entities deleted</returns>
        Task<int> DeleteBatchAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, bool hardDelete = false, CancellationToken cancellationToken = default);

        #endregion

        #region Query Operations

        /// <summary>
        /// Queries entities based on a predicate expression.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entities matching the predicate</returns>
        Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries entities with pagination support.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="orderBy">Order by expression</param>
        /// <param name="ascending">Sort direction</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Page of entities</returns>
        Task<PagedResult<T>> QueryPagedAsync(
            Expression<Func<T, bool>> predicate,
            int pageSize,
            int pageNumber,
            Expression<Func<T, IComparable>> orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts entities matching a predicate.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Count of matching entities</returns>
        Task<long> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entity exists matching a predicate.
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if any entity matches</returns>
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Imports entities in bulk from an external source.
        /// </summary>
        /// <param name="entities">The entities to import</param>
        /// <param name="options">Import options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Import result with statistics</returns>
        Task<BulkImportResult> BulkImportAsync(
            IEnumerable<T> entities,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports entities in bulk based on criteria.
        /// </summary>
        /// <param name="predicate">Filter for entities to export</param>
        /// <param name="options">Export options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with exported entities</returns>
        Task<BulkExportResult<T>> BulkExportAsync(
            Expression<Func<T, bool>> predicate = null,
            BulkExportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        #endregion

        #region Transaction Operations

        /// <summary>
        /// Begins a new transaction scope.
        /// </summary>
        /// <returns>Transaction scope for managing transactional operations</returns>
        Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Maintenance Operations

        /// <summary>
        /// Removes expired entities based on their ExpirationTime.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of entities removed</returns>
        Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Optimizes the underlying storage (e.g., vacuum, reindex).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task OptimizeStorageAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets storage statistics for monitoring.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Storage statistics</returns>
        Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion
    }

```

## C.5 Supporting Types

### Query Result Types

```csharp
    /// <summary>
    /// Represents a paged result set.
    /// </summary>
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(this.TotalCount / (double)this.PageSize);
    }
```

### Bulk Operation Options

```csharp
    /// <summary>
    /// Options for bulk import operations.
    /// </summary>
    public class BulkImportOptions
    {
        public int BatchSize { get; set; } = 1000;
        public bool IgnoreDuplicates { get; set; } = false;
        public bool ValidateBeforeImport { get; set; } = true;
        public bool UpdateExisting { get; set; } = false;
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Options for bulk export operations.
    /// </summary>
    public class BulkExportOptions
    {
        public int BatchSize { get; set; } = 1000;
        public bool IncludeDeleted { get; set; } = false;
        public string[] IncludeFields { get; set; }
        public string[] ExcludeFields { get; set; }
        public TimeSpan? Timeout { get; set; }
    }
```

### Operation Results and Progress

```csharp
    /// <summary>
    /// Progress information for bulk operations.
    /// </summary>
    public class BulkOperationProgress
    {
        public long ProcessedCount { get; set; }
        public long TotalCount { get; set; }
        public double PercentComplete => TotalCount > 0 ? (ProcessedCount * 100.0 / TotalCount) : 0;
        public TimeSpan ElapsedTime { get; set; }
        public string CurrentOperation { get; set; }
    }

    /// <summary>
    /// Result of a bulk import operation.
    /// </summary>
    public class BulkImportResult
    {
        public long SuccessCount { get; set; }
        public long FailureCount { get; set; }
        public long DuplicateCount { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of a bulk export operation.
    /// </summary>
    public class BulkExportResult<T>
    {
        public IEnumerable<T> ExportedEntities { get; set; }
        public long ExportedCount { get; set; }
        public TimeSpan Duration { get; set; }
    }
```

### Storage Statistics

```csharp
    /// <summary>
    /// Storage statistics for monitoring.
    /// </summary>
    public class StorageStatistics
    {
        public long TotalEntities { get; set; }
        public long ActiveEntities { get; set; }
        public long DeletedEntities { get; set; }
        public long ExpiredEntities { get; set; }
        public long StorageSizeBytes { get; set; }
        public Dictionary<string, long> EntitiesByType { get; set; }
        public DateTimeOffset LastOptimized { get; set; }
    }
}
```