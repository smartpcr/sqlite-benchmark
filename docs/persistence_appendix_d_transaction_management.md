# Appendix D: Transaction Management

## D.1 Transaction Interfaces

### ITransactionalOperation Interface

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Defines a transactional operation that can be committed or rolled back.
    /// Generic version supports entity-specific operations with type safety.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The key type</typeparam>
    public interface ITransactionalOperation<T, TKey> 
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the unique identifier for this operation.
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// Gets the operation description for logging.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Commits the operation with original and new values.
        /// </summary>
        /// <param name="originalValue">The original entity value (null for creates)</param>
        /// <param name="newValue">The new entity value (null for deletes)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CommitAsync(T originalValue, T newValue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the operation with original and new values.
        /// </summary>
        /// <param name="originalValue">The original entity value</param>
        /// <param name="newValue">The new entity value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RollbackAsync(T originalValue, T newValue, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Forward operation transforms TFrom to TTo
    /// </summary>
    public interface IForwardOperation<TFrom, TTo>
    {
        string OperationId { get; }
        string Description { get; }
        
        /// <summary>
        /// Transforms input to output (e.g., read entity, update entity, create entity)
        /// </summary>
        Task<TTo> ExecuteAsync(TFrom input, CancellationToken cancellationToken = default);
    }
}
```

### ITransactionScope Interface

```csharp
using System;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Defines a transaction scope that manages a collection of transactional operations.
    /// The scope is created by a persistence provider and handles SQL translation internally.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The key type</typeparam>
    public interface ITransactionScope<T, TKey> : IDisposable
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the unique transaction identifier.
        /// </summary>
        string TransactionId { get; }

        /// <summary>
        /// Gets the current state of the transaction.
        /// </summary>
        TransactionState State { get; }

        /// <summary>
        /// Gets the time when the transaction started.
        /// </summary>
        DateTimeOffset StartTime { get; }

        /// <summary>
        /// Adds a forward operation to the transaction.
        /// Operations are chained - output of one becomes input of the next.
        /// </summary>
        /// <typeparam name="TFrom">Input type</typeparam>
        /// <typeparam name="TTo">Output type</typeparam>
        /// <param name="operation">The forward operation</param>
        void AddOperation<TFrom, TTo>(IForwardOperation<TFrom, TTo> operation);
        
        /// <summary>
        /// Adds multiple operations that will be chained together.
        /// </summary>
        /// <param name="operations">List of operations to execute in order</param>
        void AddOperations(IEnumerable<object> operations);
        
        /// <summary>
        /// Executes all operations in the transaction.
        /// </summary>
        Task<bool> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
```

## D.2 Transaction Implementation

### TransactionScope Class

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Implementation of ITransactionScope that manages transactional operations.
    /// Created by persistence provider and handles SQL translation internally.
    /// </summary>
    public class TransactionScope<T, TKey> : ITransactionScope<T, TKey>
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly IPersistenceProvider<T, TKey> provider;
        private readonly List<object> forwardOperations = new List<object>();
        private readonly object lockObject = new object();
        private bool disposed;

        public string TransactionId { get; }
        public TransactionState State { get; private set; }
        public DateTimeOffset StartTime { get; }

        public TransactionScope(IPersistenceProvider<T, TKey> provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            TransactionId = Guid.NewGuid().ToString();
            State = TransactionState.Active;
            StartTime = DateTimeOffset.UtcNow;
        }

        public void AddOperation<TFrom, TTo>(IForwardOperation<TFrom, TTo> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {State} transaction.");

            lock (this.lockObject)
            {
                this.forwardOperations.Add(operation);
            }
        }
        
        public void AddOperations(IEnumerable<object> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {State} transaction.");

            lock (this.lockObject)
            {
                this.forwardOperations.AddRange(operations);
            }
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot execute a {State} transaction.");

            State = TransactionState.Committing;
            
            var reverseOperations = new Stack<(object result, object operation)>();
            object currentInput = null;

            try
            {
                // Execute forward operations in sequence, chaining outputs to inputs
                foreach (var operation in this.forwardOperations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Use reflection to invoke the operation with the correct types
                    var operationType = operation.GetType();
                    var executeMethod = operationType.GetMethod("ExecuteAsync");
                    
                    if (executeMethod == null)
                        throw new InvalidOperationException($"Operation {operationType.Name} does not implement ExecuteAsync");

                    // Execute the operation
                    var task = executeMethod.Invoke(operation, new object[] { currentInput, this.provider, cancellationToken }) as Task;
                    await task.ConfigureAwait(false);
                    
                    // Get the result
                    var resultProperty = task.GetType().GetProperty("Result");
                    currentInput = resultProperty?.GetValue(task);
                    
                    // Push onto reverse stack for potential rollback
                    reverseOperations.Push((currentInput, operation));
                }

                State = TransactionState.Committed;
                return true;
            }
            catch (Exception ex)
            {
                State = TransactionState.RollingBack;
                
                // Rollback in reverse order
                var rollbackErrors = new List<Exception>();
                
                while (reverseOperations.Count > 0)
                {
                    var (result, operation) = reverseOperations.Pop();
                    
                    try
                    {
                        // Get the reverse operation
                        var operationType = operation.GetType();
                        var reverseProperty = operationType.GetProperty("ReverseOperation");
                        
                        if (reverseProperty != null)
                        {
                            var reverseOperation = reverseProperty.GetValue(operation);
                            if (reverseOperation != null)
                            {
                                var reverseType = reverseOperation.GetType();
                                var reverseExecuteMethod = reverseType.GetMethod("ExecuteAsync");
                                
                                if (reverseExecuteMethod != null)
                                {
                                    var reverseTask = reverseExecuteMethod.Invoke(
                                        reverseOperation, 
                                        new object[] { result, this.provider, cancellationToken }) as Task;
                                    await reverseTask.ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        rollbackErrors.Add(new Exception(
                            $"Failed to rollback operation {operation.GetType().Name}: {rollbackEx.Message}", 
                            rollbackEx));
                    }
                }

                State = TransactionState.Failed;
                
                if (rollbackErrors.Any())
                {
                    throw new AggregateException(
                        $"Transaction failed with error: {ex.Message}. Additionally, {rollbackErrors.Count} rollback operations failed.",
                        rollbackErrors.Prepend(ex));
                }
                
                throw;
            }
        }


        public void Dispose()
        {
            if (this.disposed)
                return;

            if (this.State == TransactionState.Active)
            {
                // Log warning - transaction was not executed
                // In production, this would log a warning that a transaction was disposed without execution
            }

            this.disposed = true;
        }
    }
}
```

## D.3 Transaction Support Types

### TransactionState Enum

```csharp
namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Transaction states.
    /// </summary>
    public enum TransactionState
    {
        Active,
        Committing,
        Committed,
        RollingBack,
        RolledBack,
        Failed
    }
}
```

### TransactionException Class

```csharp
using System;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Exception thrown when a transaction operation fails.
    /// </summary>
    public class TransactionException : Exception
    {
        public string OperationId { get; }

        public TransactionException(string message, Exception innerException, string operationId)
            : base(message, innerException)
        {
            OperationId = operationId;
        }
    }
}
```

## D.4 Example Implementations

### EntityTransactionalOperation Class

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence
{
    /// <summary>
    /// Example implementation of ITransactionalOperation for entity operations.
    /// </summary>
    public class EntityTransactionalOperation<T, TKey> : ITransactionalOperation
        where T : IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly Func<CancellationToken, Task> commitAction;
        private readonly Func<CancellationToken, Task> rollbackAction;

        public string OperationId { get; }
        public string Description { get; }

        public EntityTransactionalOperation(
            string description,
            Func<CancellationToken, Task> commitAction,
            Func<CancellationToken, Task> rollbackAction)
        {
            OperationId = Guid.NewGuid().ToString();
            Description = description ?? throw new ArgumentNullException(nameof(description));
            this.commitAction = commitAction ?? throw new ArgumentNullException(nameof(commitAction));
            this.rollbackAction = rollbackAction ?? throw new ArgumentNullException(nameof(rollbackAction));
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return this.commitAction(cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return this.rollbackAction(cancellationToken);
        }
    }
}
```

## D.5 Usage Example

### SQLite Transaction Example with Forward/Reverse Operations

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AzureStack.Services.Update.Common.Persistence;
using Microsoft.AzureStack.Services.Update.Common.Persistence.SQLite;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Examples
{
    /// <summary>
    /// Example of using SQLitePersistenceProvider with forward/reverse operations in transactions.
    /// Shows how operations can be chained together with automatic rollback support.
    /// </summary>
    public class SQLiteTransactionExample
    {
        private readonly SQLitePersistenceProvider<UpdateEntity, string> updateProvider;
        private readonly SQLitePersistenceProvider<CacheEntry, string> cacheProvider;
        
        public SQLiteTransactionExample(string connectionString)
        {
            this.updateProvider = new SQLitePersistenceProvider<UpdateEntity, string>(
                connectionString, 
                new UpdateEntityMapper());
                
            this.cacheProvider = new SQLitePersistenceProvider<CacheEntry, string>(
                connectionString,
                new CacheEntryMapper());
        }

        /// <summary>
        /// Demonstrates chained functional operations in a transaction using forward/reverse operations.
        /// Shows how operations can be composed and results flow from one to the next.
        /// </summary>
        public async Task<bool> TransferUpdateWithCacheAsync(
            string sourceUpdateId, 
            string targetUpdateId, 
            UpdateState newState,
            CallerInfo callerInfo)
        {
            // Begin transaction - provider is passed to TransactionScope constructor
            using var tx = await this.updateProvider.BeginTransactionAsync();
            
            // Operation 1: Read source update
            var readSourceOp = new ForwardOperation<string, UpdateEntity>(
                async (input, provider, ct) =>
                {
                    // The provider handles SQL translation internally
                    // Translates to: SELECT * FROM Updates WHERE UpdateId = @key ORDER BY Version DESC LIMIT 1
                    // Then checks if IsDeleted = 1 and returns null if so
                    var sourceUpdate = await provider.GetAsync(sourceUpdateId, callerInfo, ct);
                    if (sourceUpdate == null)
                        throw new InvalidOperationException($"Source update {sourceUpdateId} not found");
                    return sourceUpdate;
                },
                // No reverse operation needed for reads
                null);
            
            // Operation 2: Update source (transforms the entity)
            var updateSourceOp = new ForwardOperation<UpdateEntity, UpdateEntity>(
                async (sourceUpdate, provider, ct) =>
                {
                    // Transform function - provider translates to SQL UPDATE
                    Func<UpdateEntity, UpdateEntity> markAsTransferred = update =>
                    {
                        update.State = UpdateState.Transferred;
                        update.Description = $"Transferred to {targetUpdateId}";
                        return update;
                    };
                    
                    // Provider translates to UPDATE SQL with optimistic concurrency
                    return await provider.UpdateAsync(sourceUpdate, markAsTransferred, callerInfo, ct);
                },
                // Reverse operation - restore original state
                new ForwardOperation<UpdateEntity, UpdateEntity>(
                    async (updatedSource, provider, ct) =>
                    {
                        // Restore original state on rollback
                        Func<UpdateEntity, UpdateEntity> restoreOriginal = update =>
                        {
                            update.State = UpdateState.Ready;
                            update.Description = "Transfer rolled back";
                            return update;
                        };
                        return await provider.UpdateAsync(updatedSource, restoreOriginal, callerInfo, ct);
                    },
                    null));
            
            // Operation 3: Create or update target
            var createOrUpdateTargetOp = new ForwardOperation<UpdateEntity, UpdateEntity>(
                async (sourceUpdate, provider, ct) =>
                {
                    // Try to get existing target
                    var targetUpdate = await provider.GetAsync(targetUpdateId, callerInfo, ct);
                    
                    if (targetUpdate == null)
                    {
                        // Create new target from source
                        targetUpdate = new UpdateEntity
                        {
                            Id = targetUpdateId,
                            UpdateName = sourceUpdate.UpdateName,
                            Version = sourceUpdate.Version,
                            Publisher = sourceUpdate.Publisher,
                            State = newState,
                            Priority = sourceUpdate.Priority,
                            PackageType = sourceUpdate.PackageType,
                            PackageSize = sourceUpdate.PackageSize,
                            ReleaseDate = sourceUpdate.ReleaseDate,
                            Dependencies = sourceUpdate.Dependencies,
                            Prerequisites = sourceUpdate.Prerequisites
                        };
                        
                        // Provider translates to INSERT SQL
                        return await provider.CreateAsync(targetUpdate, callerInfo, ct);
                    }
                    else
                    {
                        // Update existing target
                        Func<UpdateEntity, UpdateEntity> applyTransfer = update =>
                        {
                            update.State = newState;
                            update.Description = $"Transferred from {sourceUpdateId}";
                            update.Priority = sourceUpdate.Priority;
                            return update;
                        };
                        
                        // Provider translates to UPDATE SQL
                        return await provider.UpdateAsync(targetUpdate, applyTransfer, callerInfo, ct);
                    }
                },
                // Reverse operation - delete or restore target
                new ForwardOperation<UpdateEntity, UpdateEntity>(
                    async (targetUpdate, provider, ct) =>
                    {
                        if (targetUpdate.CreatedTime == targetUpdate.LastWriteTime)
                        {
                            // Was newly created, so delete it
                            return await provider.DeleteAsync(targetUpdate.Id, callerInfo, ct);
                        }
                        else
                        {
                            // Was updated, restore to previous state
                            Func<UpdateEntity, UpdateEntity> restoreTarget = update =>
                            {
                                update.State = UpdateState.Ready;
                                update.Description = "Transfer rolled back";
                                return update;
                            };
                            return await provider.UpdateAsync(targetUpdate, restoreTarget, callerInfo, ct);
                        }
                    },
                    null));
            
            // Operation 4: Create cache entry for audit
            var createCacheOp = new ForwardOperation<UpdateEntity, CacheEntry>(
                async (targetUpdate, provider, ct) =>
                {
                    var cacheEntry = new CacheEntry
                    {
                        Id = $"transfer_{sourceUpdateId}_to_{targetUpdateId}",
                        TypeName = "UpdateTransfer",
                        AssemblyVersion = "1.0.0.0",
                        Data = System.Text.Encoding.UTF8.GetBytes(
                            $"{{\"source\":\"{sourceUpdateId}\",\"target\":\"{targetUpdateId}\",\"state\":\"{newState}\"}}"),
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(30),
                        Size = 256
                    };
                    
                    // Use cache provider to create entry
                    return await this.cacheProvider.CreateAsync(cacheEntry, callerInfo, ct);
                },
                // Reverse operation - delete cache entry
                new ForwardOperation<CacheEntry, CacheEntry>(
                    async (cacheEntry, provider, ct) =>
                    {
                        return await this.cacheProvider.DeleteAsync(cacheEntry.Id, callerInfo, ct);
                    },
                    null));
            
            // Add all operations to the transaction
            tx.AddOperation(readSourceOp);
            tx.AddOperation(updateSourceOp);
            tx.AddOperation(createOrUpdateTargetOp);
            tx.AddOperation(createCacheOp);
            
            // Execute the transaction - operations are chained automatically
            return await tx.ExecuteAsync();
        }
    }
}
                    // Translates to: UPDATE Updates SET State = @State, Description = @Description, Version = @Version, LastWriteTime = @LastWriteTime 
                    //                WHERE UpdateId = @key AND Version = @originalVersion AND IsDeleted = 0
                    sourceUpdate = await this.updateProvider.UpdateAsync(sourceUpdate, markAsTransferred, callerInfo, ct);
                });
            
            // Operation 3: Read target update
            var readTargetOp = new SQLiteTransactionalOperation(
                "Read target update",
                async (ct) =>
                {
                    // Func<string, UpdateEntity> - Read by key
                    // Translates to: SELECT * FROM Updates WHERE UpdateId = @key ORDER BY Version DESC LIMIT 1
                    // Then checks if IsDeleted = 1 and returns null if so
                    targetUpdate = await this.updateProvider.GetAsync(targetUpdateId, callerInfo, ct);
                });
            
            // Operation 4: Update or create target (chains from readTarget result)
            var updateOrCreateTargetOp = new SQLiteTransactionalOperation(
                "Update or create target",
                async (ct) =>
                {
                    if (targetUpdate == null)
                    {
                        // Func<UpdateEntity, UpdateEntity> - Create new entity
                        targetUpdate = new UpdateEntity
                        {
                            Id = targetUpdateId,
                            UpdateName = sourceUpdate.UpdateName,  // Uses sourceUpdate from operation 1
                            Version = sourceUpdate.Version,
                            Publisher = sourceUpdate.Publisher,
                            State = newState,
                            Priority = sourceUpdate.Priority,
                            PackageType = sourceUpdate.PackageType,
                            PackageSize = sourceUpdate.PackageSize,
                            ReleaseDate = sourceUpdate.ReleaseDate,
                            Dependencies = sourceUpdate.Dependencies,
                            Prerequisites = sourceUpdate.Prerequisites
                        };
                        // Translates to: INSERT INTO Updates (...) VALUES (...)
                        targetUpdate = await this.updateProvider.CreateAsync(targetUpdate, callerInfo, ct);
                    }
                    else
                    {
                        // Func<UpdateEntity, UpdateEntity> - Transform existing
                        Func<UpdateEntity, UpdateEntity> applyTransfer = update =>
                        {
                            update.State = newState;
                            update.Description = $"Transferred from {sourceUpdateId}";
                            update.Priority = sourceUpdate.Priority;  // Uses sourceUpdate from operation 1
                            return update;
                        };
                        // Translates to: UPDATE Updates SET ... WHERE ...
                        targetUpdate = await this.updateProvider.UpdateAsync(targetUpdate, applyTransfer, callerInfo, ct);
                    }
                });
            
            // Operation 5: Create cache entry for audit trail
            var createCacheEntryOp = new SQLiteTransactionalOperation(
                "Create transfer audit cache entry",
                async (ct) =>
                {
                    // Action<CacheEntry> - Create audit record
                    var cacheEntry = new CacheEntry
                    {
                        Id = $"transfer:{sourceUpdateId}:{targetUpdateId}",
                        TypeName = "UpdateTransfer",
                        AssemblyVersion = "1.0.0.0",
                        Data = System.Text.Encoding.UTF8.GetBytes(
                            $"{{\"source\":\"{sourceUpdate.Id}\",\"target\":\"{targetUpdate.Id}\"," +
                            $"\"sourceState\":\"{sourceUpdate.State}\",\"targetState\":\"{targetUpdate.State}\"," +
                            $"\"sourceVersion\":\"{sourceUpdate.Version}\",\"targetVersion\":\"{targetUpdate.Version}\"," +
                            $"\"timestamp\":\"{DateTime.UtcNow:O}\"}}"),
                        Size = 512,
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(7)
                    };
                    // Translates to: INSERT INTO CacheEntry (...) VALUES (...)
                    await this.cacheProvider.CreateAsync(cacheEntry, callerInfo, ct);
                });
            
            // Add all operations to transaction scope - they execute in order with chained results
            tx.AddOperation(readSourceOp);        // Step 1: Read source update
            tx.AddOperation(updateSourceOp);      // Step 2: Update source (uses result from step 1)
            tx.AddOperation(readTargetOp);        // Step 3: Read target update
            tx.AddOperation(updateOrCreateTargetOp); // Step 4: Update/create target (uses results from steps 1 & 3)
            tx.AddOperation(createCacheEntryOp);  // Step 5: Create cache entry (uses results from all previous steps)
            
            // Execute all operations in order - transaction commits automatically on dispose
            // Any exception in any operation causes complete rollback
            return true;
        }

        /// <summary>
        /// Example of batch operations with complex queries using functional approach.
        /// </summary>
        public async Task<int> ArchiveOldUpdatesAsync(
            int daysOld,
            CallerInfo callerInfo)
        {
            using var transactionScope = await this.updateProvider.BeginTransactionAsync();
            
            try
            {
                // Query for old updates using expression
                // Translates to: SELECT * FROM Updates WHERE State = 'Succeeded' AND LastWriteTime < @cutoffTime AND IsDeleted = 0
                var cutoffTime = DateTimeOffset.UtcNow.AddDays(-daysOld);
                Expression<Func<UpdateEntity, bool>> oldSuccessfulUpdates = 
                    u => u.State == UpdateState.Succeeded && 
                         u.LastWriteTime < cutoffTime &&
                         !u.IsDeleted;
                
                var updatesToArchive = await this.updateProvider.QueryAsync(oldSuccessfulUpdates, callerInfo);
                
                int archivedCount = 0;
                foreach (var update in updatesToArchive)
                {
                    // Archive to cache with metadata
                    var archiveEntry = new CacheEntry
                    {
                        Id = $"archive:update:{update.Id}",
                        TypeName = "ArchivedUpdate",
                        AssemblyVersion = "1.0.0.0",
                        Data = SerializeUpdate(update),
                        Size = 1024, // Approximate
                        AbsoluteExpiration = null // No expiration for archives
                    };
                    
                    // Create archive entry
                    // Translates to: INSERT INTO CacheEntry (...) VALUES (...)
                    await this.cacheProvider.CreateAsync(archiveEntry, callerInfo);
                    
                    // Soft delete the original update
                    // Translates to: UPDATE Updates SET IsDeleted = 1, LastWriteTime = @lastWriteTime WHERE UpdateId = @key AND IsDeleted = 0
                    await this.updateProvider.DeleteAsync(update.Id, callerInfo, hardDelete: false);
                    
                    archivedCount++;
                }
                
                // Create summary cache entry
                var summaryEntry = new CacheEntry
                {
                    Id = $"archive:summary:{DateTime.UtcNow:yyyyMMddHHmmss}",
                    TypeName = "ArchiveSummary",
                    AssemblyVersion = "1.0.0.0",
                    Data = System.Text.Encoding.UTF8.GetBytes(
                        $"{{\"archivedCount\":{archivedCount},\"cutoffDate\":\"{cutoffTime:O}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}"),
                    Size = 128,
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(30)
                };
                
                await this.cacheProvider.CreateAsync(summaryEntry, callerInfo);
                
                return archivedCount;
            }
            catch
            {
                // Automatic rollback on exception
                throw;
            }
        }
        
        private byte[] SerializeUpdate(UpdateEntity update)
        {
            // Use the serializer resolved based on UpdateEntity's attributes
            var serializer = SerializerResolver.CreateSerializer<UpdateEntity>();
            return serializer.Serialize(update);
        }
    }

    /// <summary>
    /// SQLite-specific implementation of ITransactionalOperation.
    /// </summary>
    public class SQLiteTransactionalOperation : ITransactionalOperation
    {
        private readonly Func<CancellationToken, Task> commitAction;
        private readonly Func<CancellationToken, Task> rollbackAction;

        public string OperationId { get; }
        public string Description { get; }

        public SQLiteTransactionalOperation(
            string description,
            Func<CancellationToken, Task> commitAction,
            Func<CancellationToken, Task> rollbackAction)
        {
            OperationId = Guid.NewGuid().ToString();
            Description = description ?? throw new ArgumentNullException(nameof(description));
            this.commitAction = commitAction ?? throw new ArgumentNullException(nameof(commitAction));
            this.rollbackAction = rollbackAction ?? throw new ArgumentNullException(nameof(rollbackAction));
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return this.commitAction(cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return this.rollbackAction(cancellationToken);
        }
    }
}
```