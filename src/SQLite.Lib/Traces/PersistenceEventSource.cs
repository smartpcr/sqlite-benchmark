// -----------------------------------------------------------------------
// <copyright file="PersistenceEventSource.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Traces
{
    using System;
    using System.Diagnostics.Tracing;

    /// <summary>
    /// Event source for SQLite persistence provider ETW tracing.
    /// </summary>
    [EventSource(Name = "Microsoft-AzureStack-Persistence")]
    internal sealed class PersistenceEventSource : EventSource
    {
        /// <summary>
        /// Singleton instance of the event source.
        /// </summary>
        public static readonly PersistenceEventSource Log = new PersistenceEventSource();

        private PersistenceEventSource()
        {
        }

        /// <summary>
        /// Keywords for categorizing events.
        /// </summary>
        public static class Keywords
        {
            public const EventKeywords Database = (EventKeywords)0x0001;
            public const EventKeywords Query = (EventKeywords)0x0002;
            public const EventKeywords Transaction = (EventKeywords)0x0004;
            public const EventKeywords Performance = (EventKeywords)0x0008;
            public const EventKeywords Error = (EventKeywords)0x0010;
            public const EventKeywords Batch = (EventKeywords)0x0020;
            public const EventKeywords Bulk = (EventKeywords)0x0040;
            public const EventKeywords Cache = (EventKeywords)0x0080;
        }

        /// <summary>
        /// Tasks for grouping related events.
        /// </summary>
        public static class Tasks
        {
            public const EventTask Create = (EventTask)1;
            public const EventTask Read = (EventTask)2;
            public const EventTask Update = (EventTask)3;
            public const EventTask Delete = (EventTask)4;
            public const EventTask Query = (EventTask)5;
            public const EventTask Transaction = (EventTask)6;
            public const EventTask Batch = (EventTask)7;
            public const EventTask Bulk = (EventTask)8;
            public const EventTask Maintenance = (EventTask)9;
        }

        #region Create Operations

        [Event(1, Level = EventLevel.Informational, Keywords = Keywords.Database, Task = Tasks.Create,
            Message = "Creating entity with key {0} in table {1} [Called from {2}.{3}:{4}]")]
        public void CreateStart(string key, string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(1, key, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Performance, Task = Tasks.Create,
            Message = "Created entity with key {0} in table {1} in {2}ms [Called from {3}.{4}:{5}]")]
        public void CreateStop(string key, string tableName, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(2, key, tableName, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(3, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Error, Task = Tasks.Create,
            Message = "Failed to create entity with key {0} in table {1} in {2}ms. Exception: {3} - {4} [Called from {5}.{6}:{7}]")]
        public void CreateFailed(string key, string tableName, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(3, key, tableName, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Read Operations

        [Event(4, Level = EventLevel.Informational, Keywords = Keywords.Database, Task = Tasks.Read,
            Message = "Getting entity with key {0} from table {1} [Called from {2}.{3}:{4}]")]
        public void GetStart(string key, string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(4, key, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(5, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Performance, Task = Tasks.Read,
            Message = "Got entity with key {0} from table {1} in {2}ms [Called from {3}.{4}:{5}]")]
        public void GetStop(string key, string tableName, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(5, key, tableName, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(6, Level = EventLevel.Warning, Keywords = Keywords.Database, Task = Tasks.Read,
            Message = "Entity with key {0} not found in table {1} [Called from {2}.{3}:{4}]")]
        public void GetNotFound(string key, string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(6, key, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(7, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Error, Task = Tasks.Read,
            Message = "Failed to get entity with key {0} from table {1} in {2}ms. Exception: {3} - {4} [Called from {5}.{6}:{7}]")]
        public void GetFailed(string key, string tableName, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(7, key, tableName, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Update Operations

        [Event(8, Level = EventLevel.Informational, Keywords = Keywords.Database, Task = Tasks.Update,
            Message = "Updating entity with key {0} in table {1} [Called from {2}.{3}:{4}]")]
        public void UpdateStart(string key, string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(8, key, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(9, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Performance, Task = Tasks.Update,
            Message = "Updated entity with key {0} in table {1} in {2}ms [Called from {3}.{4}:{5}]")]
        public void UpdateStop(string key, string tableName, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(9, key, tableName, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(10, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Error, Task = Tasks.Update,
            Message = "Failed to update entity with key {0} in table {1} in {2}ms. Exception: {3} - {4} [Called from {5}.{6}:{7}]")]
        public void UpdateFailed(string key, string tableName, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(10, key, tableName, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(11, Level = EventLevel.Warning, Keywords = Keywords.Database, Task = Tasks.Update,
            Message = "Concurrency conflict updating entity with key {0} in table {1} [Called from {2}.{3}:{4}]")]
        public void UpdateConcurrencyConflict(string key, string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(11, key, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Delete Operations

        [Event(12, Level = EventLevel.Informational, Keywords = Keywords.Database, Task = Tasks.Delete,
            Message = "Deleting entity with key {0} from table {1} [Called from {2}.{3}:{4}]")]
        public void DeleteStart(string key, string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(12, key, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(13, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Performance, Task = Tasks.Delete,
            Message = "Deleted entity with key {0} from table {1} in {2}ms [Called from {3}.{4}:{5}]")]
        public void DeleteStop(string key, string tableName, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(13, key, tableName, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(14, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Error, Task = Tasks.Delete,
            Message = "Failed to delete entity with key {0} from table {1} in {2}ms. Exception: {3} - {4} [Called from {5}.{6}:{7}]")]
        public void DeleteFailed(string key, string tableName, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(14, key, tableName, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Batch Operations

        [Event(15, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Batch, Task = Tasks.Batch,
            Message = "Starting batch operation {0} with {1} items for list {2} [Called from {3}.{4}:{5}]")]
        public void BatchOperationStart(string operation, int count, string listCacheKey, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(15, operation, count, listCacheKey, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(16, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Batch | Keywords.Performance, Task = Tasks.Batch,
            Message = "Completed batch operation {0} with {1} items for list {2} in {3}ms [Called from {4}.{5}:{6}]")]
        public void BatchOperationStop(string operation, int count, string listCacheKey, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(16, operation, count, listCacheKey, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(17, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Batch | Keywords.Error, Task = Tasks.Batch,
            Message = "Failed batch operation {0} for list {1} in {2}ms. Exception: {3} - {4} [Called from {5}.{6}:{7}]")]
        public void BatchOperationFailed(string operation, string listCacheKey, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(17, operation, listCacheKey, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Query Operations

        [Event(18, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Query, Task = Tasks.Query,
            Message = "Executing query on table {0} [Called from {1}.{2}:{3}]")]
        public void QueryStart(string tableName, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(18, tableName, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(19, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Query | Keywords.Performance, Task = Tasks.Query,
            Message = "Query on table {0} returned {1} results in {2}ms [Called from {3}.{4}:{5}]")]
        public void QueryStop(string tableName, int resultCount, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(19, tableName, resultCount, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(20, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Query | Keywords.Error, Task = Tasks.Query,
            Message = "Query failed on table {0} in {1}ms. Exception: {2} - {3} [Called from {4}.{5}:{6}]")]
        public void QueryFailed(string tableName, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(20, tableName, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Transaction Operations

        [Event(21, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Transaction, Task = Tasks.Transaction,
            Message = "Starting transaction [Called from {0}.{1}:{2}]")]
        public void TransactionStart(string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(21, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(22, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Transaction, Task = Tasks.Transaction,
            Message = "Committing transaction [Called from {0}.{1}:{2}]")]
        public void TransactionCommit(string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(22, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(23, Level = EventLevel.Warning, Keywords = Keywords.Database | Keywords.Transaction, Task = Tasks.Transaction,
            Message = "Rolling back transaction [Called from {0}.{1}:{2}]")]
        public void TransactionRollback(string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(23, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(24, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Transaction | Keywords.Error, Task = Tasks.Transaction,
            Message = "Transaction failed. Exception: {0} - {1} [Called from {2}.{3}:{4}]")]
        public void TransactionFailed(string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(24, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Bulk Operations

        [Event(25, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Bulk, Task = Tasks.Bulk,
            Message = "Starting bulk {0} operation with {1} entities [Called from {2}.{3}:{4}]")]
        public void BulkOperationStart(string operation, int count, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(25, operation, count, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(26, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Bulk | Keywords.Performance, Task = Tasks.Bulk,
            Message = "Completed bulk {0} operation with {1} entities in {2}ms [Called from {3}.{4}:{5}]")]
        public void BulkOperationStop(string operation, int count, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(26, operation, count, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(27, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Bulk, Task = Tasks.Bulk,
            Message = "Bulk operation progress: {0}% complete ({1}/{2} entities) [Called from {3}.{4}:{5}]")]
        public void BulkOperationProgress(int percentComplete, long processed, long total, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(27, percentComplete, processed, total, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(28, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Bulk | Keywords.Error, Task = Tasks.Bulk,
            Message = "Bulk {0} operation failed in {1}ms. Exception: {2} - {3} [Called from {4}.{5}:{6}]")]
        public void BulkOperationFailed(string operation, long elapsedMilliseconds, string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(28, operation, elapsedMilliseconds, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Cache Operations

        [Event(29, Level = EventLevel.Verbose, Keywords = Keywords.Cache, 
            Message = "Cache hit for key {0} [Called from {1}.{2}:{3}]")]
        public void CacheHit(string key, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(29, key, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(30, Level = EventLevel.Verbose, Keywords = Keywords.Cache,
            Message = "Cache miss for key {0} [Called from {1}.{2}:{3}]")]
        public void CacheMiss(string key, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(30, key, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region Maintenance Operations

        [Event(31, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Performance, Task = Tasks.Maintenance,
            Message = "Starting storage optimization [Called from {0}.{1}:{2}]")]
        public void OptimizeStorageStart(string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(31, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(32, Level = EventLevel.Informational, Keywords = Keywords.Database | Keywords.Performance, Task = Tasks.Maintenance,
            Message = "Completed storage optimization in {0}ms [Called from {1}.{2}:{3}]")]
        public void OptimizeStorageStop(long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(32, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion

        #region General Operations

        [Event(33, Level = EventLevel.Verbose, Keywords = Keywords.Database,
            Message = "Executing SQL: {0} [Called from {1}.{2}:{3}]")]
        public void SqlExecute(string sql, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(33, sql, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(34, Level = EventLevel.Error, Keywords = Keywords.Database | Keywords.Error,
            Message = "SQL execution failed. Exception: {0} - {1} [Called from {2}.{3}:{4}]")]
        public void SqlExecuteFailed(string exceptionType, string exceptionMessage, string stackTrace, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(34, exceptionType, exceptionMessage, stackTrace, callerFile ?? "", callerMember ?? "", callerLine);
        }

        [Event(35, Level = EventLevel.Warning, Keywords = Keywords.Database | Keywords.Performance,
            Message = "Slow query detected on table {0}: {1}ms [Called from {2}.{3}:{4}]")]
        public void SlowQuery(string tableName, long elapsedMilliseconds, string callerFile, string callerMember, int callerLine)
        {
            this.WriteEvent(35, tableName, elapsedMilliseconds, callerFile ?? "", callerMember ?? "", callerLine);
        }

        #endregion
    }
}