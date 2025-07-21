// -----------------------------------------------------------------------
// <copyright file="SQLiteCacheProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Threading;
    using System.Threading.Tasks;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Entities.Caches;

    /// <summary>
    /// SQLite implementation of a cache provider that stores CacheEntry&lt;T&gt; objects.
    /// </summary>
    /// <typeparam name="T">The type of value to cache</typeparam>
    public class SQLiteCacheProvider<T> : ICacheProvider<T> where T : class, IEntity<string>
    {
        private readonly SQLitePersistenceProvider<CacheEntry<T>, string> persistenceProvider;
        private readonly string connectionString;
        private readonly string tableName;
        private readonly CacheEntryMapper<T> mapper;
        private readonly TimeSpan defaultExpiration;

        public SQLiteCacheProvider(
            string connectionString,
            string tableName = "CacheEntry",
            TimeSpan? defaultExpiration = null)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.tableName = tableName;
            this.defaultExpiration = defaultExpiration ?? TimeSpan.FromHours(1);

            // Create mapper and persistence provider
            this.mapper = new CacheEntryMapper<T>(null, tableName);
            this.persistenceProvider = new SQLitePersistenceProvider<CacheEntry<T>, string>(
                connectionString,
                this.mapper);
        }

        /// <summary>
        /// Gets a cached value by key.
        /// </summary>
        public async Task<T> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            var callerInfo = new CallerInfo
            {
                CallerMemberName = nameof(GetAsync),
                CallerFilePath = "SQLiteCacheProvider.cs",
                CallerLineNumber = 0
            };

            var entry = await this.persistenceProvider.GetAsync(key, callerInfo, cancellationToken);

            if (entry == null || entry.IsDeleted)
                return null;

            // Check expiration
            if (entry.ExpirationTime.HasValue && entry.ExpirationTime.Value < DateTimeOffset.UtcNow)
            {
                // Entry has expired, soft delete it
                await this.persistenceProvider.DeleteAsync(key, callerInfo, false, cancellationToken);
                return null;
            }

            // Handle sliding expiration
            if (entry.SlidingExpiration.HasValue)
            {
                entry.RefreshExpiration();
                entry.ExpirationTime = DateTimeOffset.UtcNow.Add(entry.SlidingExpiration.Value);
                entry.AbsoluteExpiration = entry.ExpirationTime;
            }

            await this.persistenceProvider.UpdateAsync(entry, callerInfo, cancellationToken);

            return entry.Value;
        }

        /// <summary>
        /// Sets a value in the cache with the specified key.
        /// </summary>
        public async Task SetAsync(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var effectiveExpiration = expiration ?? this.defaultExpiration;
            var expirationTime = DateTimeOffset.UtcNow.Add(effectiveExpiration);

            var callerInfo = new CallerInfo
            {
                CallerMemberName = nameof(SetAsync),
                CallerFilePath = "SQLiteCacheProvider.cs",
                CallerLineNumber = 0
            };

            // Check if entry already exists
            var existingEntry = await this.persistenceProvider.GetAsync(key, callerInfo, cancellationToken);

            if (existingEntry != null && !existingEntry.IsDeleted)
            {
                // Update existing entry
                existingEntry.Value = value;
                existingEntry.TypeName = typeof(T).Name;
                existingEntry.AssemblyQualifiedName = typeof(T).AssemblyQualifiedName;
                existingEntry.Size = EstimateSize(value);
                existingEntry.ExpirationTime = expirationTime;
                existingEntry.AbsoluteExpiration = expirationTime;
                existingEntry.LastWriteTime = DateTimeOffset.UtcNow;

                await this.persistenceProvider.UpdateAsync(existingEntry, callerInfo, cancellationToken);
            }
            else
            {
                // Create new entry
                var entry = new CacheEntry<T>
                {
                    Id = key,
                    Value = value,
                    TypeName = typeof(T).Name,
                    AssemblyQualifiedName = typeof(T).AssemblyQualifiedName,
                    Size = EstimateSize(value),
                    ExpirationTime = expirationTime,
                    AbsoluteExpiration = expirationTime,
                    Tags = new string[0],
                    CreatedTime = DateTimeOffset.UtcNow,
                    LastWriteTime = DateTimeOffset.UtcNow,
                    Version = 1,
                    IsDeleted = false
                };

                await this.persistenceProvider.CreateAsync(entry, callerInfo, cancellationToken);
            }
        }

        /// <summary>
        /// Sets a value in the cache with sliding expiration.
        /// </summary>
        public async Task SetWithSlidingExpirationAsync(
            string key,
            T value,
            TimeSpan slidingExpiration,
            TimeSpan? absoluteExpiration = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var callerInfo = new CallerInfo
            {
                CallerMemberName = nameof(SetWithSlidingExpirationAsync),
                CallerFilePath = "SQLiteCacheProvider.cs",
                CallerLineNumber = 0
            };

            var now = DateTimeOffset.UtcNow;
            var expirationTime = now.Add(slidingExpiration);
            var absoluteExpirationTime = absoluteExpiration.HasValue
                ? now.Add(absoluteExpiration.Value)
                : (DateTimeOffset?)null;

            // Check if entry already exists
            var existingEntry = await this.persistenceProvider.GetAsync(key, callerInfo, cancellationToken);

            if (existingEntry != null && !existingEntry.IsDeleted)
            {
                // Update existing entry
                existingEntry.Value = value;
                existingEntry.TypeName = typeof(T).Name;
                existingEntry.AssemblyQualifiedName = typeof(T).AssemblyQualifiedName;
                existingEntry.Size = EstimateSize(value);
                existingEntry.SlidingExpiration = slidingExpiration;
                existingEntry.ExpirationTime = expirationTime;
                existingEntry.AbsoluteExpiration = absoluteExpirationTime ?? expirationTime;
                existingEntry.LastWriteTime = now;

                await this.persistenceProvider.UpdateAsync(existingEntry, callerInfo, cancellationToken);
            }
            else
            {
                // Create new entry with sliding expiration
                var entry = new CacheEntry<T>
                {
                    Id = key,
                    Value = value,
                    TypeName = typeof(T).Name,
                    AssemblyQualifiedName = typeof(T).AssemblyQualifiedName,
                    Size = EstimateSize(value),
                    SlidingExpiration = slidingExpiration,
                    ExpirationTime = expirationTime,
                    AbsoluteExpiration = absoluteExpirationTime ?? expirationTime,
                    Tags = new string[0],
                    CreatedTime = now,
                    LastWriteTime = now,
                    Version = 1,
                    IsDeleted = false
                };

                await this.persistenceProvider.CreateAsync(entry, callerInfo, cancellationToken);
            }
        }

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            var callerInfo = new CallerInfo
            {
                CallerMemberName = nameof(RemoveAsync),
                CallerFilePath = "SQLiteCacheProvider.cs",
                CallerLineNumber = 0
            };

            // Soft delete the entry
            return await this.persistenceProvider.DeleteAsync(key, callerInfo, false, cancellationToken);
        }

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            var callerInfo = new CallerInfo
            {
                CallerMemberName = nameof(ExistsAsync),
                CallerFilePath = "SQLiteCacheProvider.cs",
                CallerLineNumber = 0
            };

            var entry = await this.persistenceProvider.GetAsync(key, callerInfo, cancellationToken);

            if (entry == null || entry.IsDeleted)
                return false;

            // Check expiration
            if (entry.ExpirationTime.HasValue && entry.ExpirationTime.Value < DateTimeOffset.UtcNow)
            {
                // Entry has expired
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets all cache entries with a specific tag.
        /// </summary>
        public async Task<IList<CacheEntry<T>>> GetByTagAsync(
            string tag,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentNullException(nameof(tag));

            // Use raw SQL query to find entries with the tag
            var sql = $@"
                SELECT {string.Join(", ", this.mapper.GetSelectColumns())}
                FROM {this.tableName}
                WHERE Tags LIKE @tag
                  AND IsDeleted = 0
                  AND (AbsoluteExpiration IS NULL OR AbsoluteExpiration > @now)
                ORDER BY LastAccessTime DESC";

            var results = new List<CacheEntry<T>>();

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@tag", $"%{tag}%");
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var entry = this.mapper.MapFromReader(reader);
                if (entry.Tags != null && Array.IndexOf(entry.Tags, tag) >= 0)
                {
                    results.Add(entry);
                }
            }

            return results;
        }

        /// <summary>
        /// Clears all expired entries from the cache.
        /// </summary>
        public async Task<int> ClearExpiredAsync(CancellationToken cancellationToken = default)
        {
            var sql = $@"
                UPDATE {this.tableName}
                SET IsDeleted = 1,
                    LastWriteTime = @now
                WHERE IsDeleted = 0
                  AND AbsoluteExpiration IS NOT NULL
                  AND AbsoluteExpiration < @now;

                SELECT changes();";

            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var rowsAffected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return rowsAffected;
        }

        /// <summary>
        /// Initializes the cache database schema.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            // Create Version table first (referenced by CacheEntry)
            var createVersionTableSql = @"
                CREATE TABLE IF NOT EXISTS Version (
                    Version INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL DEFAULT (datetime('now'))
                );";

            using (var cmd = new SQLiteCommand(createVersionTableSql, connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Create CacheEntity table (referenced by CacheEntry)
            var createCacheEntityTableSql = @"
                CREATE TABLE IF NOT EXISTS CacheEntity (
                    TypeName TEXT NOT NULL,
                    AssemblyVersion TEXT NOT NULL,
                    SerializationType TEXT NOT NULL DEFAULT 'JSON',
                    Description TEXT,
                    CreatedTime TEXT NOT NULL DEFAULT (datetime('now')),
                    PRIMARY KEY (TypeName, AssemblyVersion)
                );";

            using (var cmd = new SQLiteCommand(createCacheEntityTableSql, connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Create CacheEntry table
            var createTableSql = this.mapper.GenerateCreateTableSql();
            using (var cmd = new SQLiteCommand(createTableSql, connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Create indexes
            foreach (var indexSql in this.mapper.GenerateCreateIndexSql())
            {
                using var indexCmd = new SQLiteCommand(indexSql, connection);
                await indexCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Estimates the size of a value in bytes.
        /// </summary>
        private long EstimateSize(T value)
        {
            if (value == null)
                return 0;

            try
            {
                // Use the mapper's serialization to estimate size
                var tempEntry = new CacheEntry<T> { Value = value };
                var serialized = this.mapper.SerializeCacheEntry(tempEntry);
                return serialized?.Length ?? 0;
            }
            catch
            {
                // Fallback to a rough estimate
                return 1024; // 1KB default
            }
        }
    }
}