// -----------------------------------------------------------------------
// <copyright file="Logger.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Traces
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Static logger that wraps the PersistenceEventSource for easier usage.
    /// </summary>
    public static class Logger
    {
        private static readonly PersistenceEventSource EventSource = PersistenceEventSource.Log;

        #region Create Operations

        /// <summary>
        /// Logs the start of a create operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void CreateStart(string key, string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.CreateStart(key, tableName, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of a create operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void CreateStop(string key, string tableName, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.CreateStop(key, tableName, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed create operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void CreateFailed(string key, string tableName, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.CreateFailed(key, tableName, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Read Operations

        /// <summary>
        /// Logs the start of a get operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void GetStart(string key, string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.GetStart(key, tableName, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of a get operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void GetStop(string key, string tableName, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.GetStop(key, tableName, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs when an entity is not found.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void GetNotFound(string key, string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.GetNotFound(key, tableName, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed get operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void GetFailed(string key, string tableName, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.GetFailed(key, tableName, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Update Operations

        /// <summary>
        /// Logs the start of an update operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void UpdateStart(string key, string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.UpdateStart(key, tableName, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of an update operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void UpdateStop(string key, string tableName, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.UpdateStop(key, tableName, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed update operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void UpdateFailed(string key, string tableName, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.UpdateFailed(key, tableName, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a concurrency conflict during update.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void UpdateConcurrencyConflict(string key, string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.UpdateConcurrencyConflict(key, tableName, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Delete Operations

        /// <summary>
        /// Logs the start of a delete operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void DeleteStart(string key, string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.DeleteStart(key, tableName, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of a delete operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void DeleteStop(string key, string tableName, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.DeleteStop(key, tableName, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed delete operation.
        /// </summary>
        /// <param name="key">The entity key</param>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void DeleteFailed(string key, string tableName, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.DeleteFailed(key, tableName, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Logs the start of a batch operation.
        /// </summary>
        /// <param name="operation">The operation name</param>
        /// <param name="count">The number of items in the batch</param>
        /// <param name="listCacheKey">The list cache key</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BatchOperationStart(string operation, int count, string listCacheKey,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BatchOperationStart(operation, count, listCacheKey, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of a batch operation.
        /// </summary>
        /// <param name="operation">The operation name</param>
        /// <param name="count">The number of items in the batch</param>
        /// <param name="listCacheKey">The list cache key</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BatchOperationStop(string operation, int count, string listCacheKey, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BatchOperationStop(operation, count, listCacheKey, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed batch operation.
        /// </summary>
        /// <param name="operation">The operation name</param>
        /// <param name="listCacheKey">The list cache key</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BatchOperationFailed(string operation, string listCacheKey, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BatchOperationFailed(operation, listCacheKey, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Logs the start of a query operation.
        /// </summary>
        /// <param name="tableName">The table name</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void QueryStart(string tableName,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.QueryStart(tableName, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of a query operation.
        /// </summary>
        /// <param name="tableName">The table name</param>
        /// <param name="resultCount">The number of results returned</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void QueryStop(string tableName, int resultCount, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.QueryStop(tableName, resultCount, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed query operation.
        /// </summary>
        /// <param name="tableName">The table name</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void QueryFailed(string tableName, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.QueryFailed(tableName, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Transaction Operations

        /// <summary>
        /// Logs the start of a transaction.
        /// </summary>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void TransactionStart(
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.TransactionStart(callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the commit of a transaction.
        /// </summary>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void TransactionCommit(
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.TransactionCommit(callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the rollback of a transaction.
        /// </summary>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void TransactionRollback(
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.TransactionRollback(callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed transaction.
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void TransactionFailed(Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.TransactionFailed(exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Logs the start of a bulk operation.
        /// </summary>
        /// <param name="operation">The operation name (Import/Export)</param>
        /// <param name="count">The number of entities</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BulkOperationStart(string operation, int count,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BulkOperationStart(operation, count, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of a bulk operation.
        /// </summary>
        /// <param name="operation">The operation name (Import/Export)</param>
        /// <param name="count">The number of entities</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BulkOperationStop(string operation, int count, Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BulkOperationStop(operation, count, stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the progress of a bulk operation.
        /// </summary>
        /// <param name="percentComplete">The percentage complete</param>
        /// <param name="processed">The number of entities processed</param>
        /// <param name="total">The total number of entities</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BulkOperationProgress(int percentComplete, long processed, long total,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BulkOperationProgress(percentComplete, processed, total, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a failed bulk operation.
        /// </summary>
        /// <param name="operation">The operation name (Import/Export)</param>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void BulkOperationFailed(string operation, Stopwatch stopwatch, Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.BulkOperationFailed(operation, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Cache Operations

        /// <summary>
        /// Logs a cache hit.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void CacheHit(string key,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.CacheHit(key, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a cache miss.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void CacheMiss(string key,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.CacheMiss(key, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Maintenance Operations

        /// <summary>
        /// Logs the start of storage optimization.
        /// </summary>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void OptimizeStorageStart(
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.OptimizeStorageStart(callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs the completion of storage optimization.
        /// </summary>
        /// <param name="stopwatch">The stopwatch measuring the operation duration</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void OptimizeStorageStop(Stopwatch stopwatch,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.OptimizeStorageStop(stopwatch.ElapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        #endregion

        #region General Operations

        /// <summary>
        /// Logs SQL execution.
        /// </summary>
        /// <param name="sql">The SQL statement</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void SqlExecute(string sql,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.SqlExecute(sql, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs SQL execution failure.
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void SqlExecuteFailed(Exception exception,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.SqlExecuteFailed(exception.GetType().Name, exception.Message, exception.StackTrace ?? string.Empty, callerFile, callerMember, callerLine);
        }

        /// <summary>
        /// Logs a slow query warning.
        /// </summary>
        /// <param name="tableName">The table name</param>
        /// <param name="elapsedMilliseconds">The elapsed time in milliseconds</param>
        /// <param name="callerFile">The file path of the caller (automatically populated)</param>
        /// <param name="callerMember">The member name of the caller (automatically populated)</param>
        /// <param name="callerLine">The line number of the caller (automatically populated)</param>
        public static void SlowQuery(string tableName, long elapsedMilliseconds,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            EventSource.SlowQuery(tableName, elapsedMilliseconds, callerFile, callerMember, callerLine);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Executes an operation with automatic logging.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="operation">The operation name</param>
        /// <param name="action">The action to execute</param>
        /// <param name="logStart">The start logging action</param>
        /// <param name="logStop">The stop logging action</param>
        /// <param name="logError">The error logging action</param>
        /// <returns>The result of the operation</returns>
        public static T TrackOperation<T>(
            string operation,
            Func<T> action,
            Action logStart,
            Action<Stopwatch> logStop,
            Action<Exception> logError)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                logStart();
                var result = action();
                stopwatch.Stop();
                logStop(stopwatch);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logError(ex);
                throw;
            }
        }

        /// <summary>
        /// Executes an async operation with automatic logging.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="operation">The operation name</param>
        /// <param name="action">The async action to execute</param>
        /// <param name="logStart">The start logging action</param>
        /// <param name="logStop">The stop logging action</param>
        /// <param name="logError">The error logging action</param>
        /// <returns>The result of the operation</returns>
        public static async System.Threading.Tasks.Task<T> TrackOperationAsync<T>(
            string operation,
            Func<System.Threading.Tasks.Task<T>> action,
            Action logStart,
            Action<Stopwatch> logStop,
            Action<Exception> logError)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                logStart();
                var result = await action();
                stopwatch.Stop();
                logStop(stopwatch);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logError(ex);
                throw;
            }
        }

        #endregion
    }
}