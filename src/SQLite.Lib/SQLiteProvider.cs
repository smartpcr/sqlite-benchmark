using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SQLite.Lib.Contracts;

namespace SQLite.Lib
{

    /// <summary>
    /// Generic SQLite provider for CRUD operations
    /// </summary>
    public class SQLiteProvider : IPersistenceProvider
    {
        private readonly string connectionString;
        private readonly ILogger<SQLiteProvider> logger;
        private SQLiteConnection _currentConnection;
        private SQLiteTransaction _currentTransaction;

        public SQLiteProvider(string connectionString, ILogger<SQLiteProvider> logger = null)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.logger = logger;

            this.Initialize();
        }

        private void Initialize()
        {
            using (var connection = this.CreateConnection())
            {
                connection.Open();

                // Enable foreign keys
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA foreign_keys = ON;";
                    cmd.ExecuteNonQuery();
                }

                // Set journal mode to WAL for better concurrency
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode = WAL;";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private SQLiteConnection CreateConnection()
        {
            return new SQLiteConnection(this.connectionString);
        }

        private SQLiteConnection GetConnection()
        {
            return this._currentConnection ?? this.CreateConnection();
        }

        public void CreateTable()
        {
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {this._tableName} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Data TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_{this._tableName}_created ON {this._tableName}(CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_{this._tableName}_updated ON {this._tableName}(UpdatedAt);";

            this.ExecuteCommand(sql);
            this.logger?.LogInformation($"Table {this._tableName} created or verified");
        }

        public T Insert(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var json = JsonConvert.SerializeObject(entity);
            var sql = $"INSERT INTO {this._tableName} (Data) VALUES (@data); SELECT last_insert_rowid();";

            var connection = this.GetConnection();
            var isTransactionConnection = connection == this._currentConnection;
            var shouldClose = !isTransactionConnection && connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@data", json);
                    cmd.Transaction = this._currentTransaction;

                    var id = Convert.ToInt64(cmd.ExecuteScalar());
                    this._idProperty.SetValue(entity, id);

                    this.logger?.LogDebug($"Inserted entity with ID {id} into {this._tableName}");
                    return entity;
                }
            }
            finally
            {
                if (shouldClose && !isTransactionConnection)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        public int InsertBatch(IEnumerable<T> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            var entityList = entities.ToList();
            if (!entityList.Any())
            {
                return 0;
            }

            var count = 0;
            var wasInTransaction = this._currentTransaction != null;

            if (!wasInTransaction)
            {
                using (this.BeginTransaction())
                {
                    count = this.InsertBatchInternal(entityList);
                }
            }
            else
            {
                count = this.InsertBatchInternal(entityList);
            }

            this.logger?.LogInformation($"Batch inserted {count} entities into {this._tableName}");
            return count;
        }

        private int InsertBatchInternal(List<T> entities)
        {
            var sql = $"INSERT INTO {this._tableName} (Data) VALUES (@data)";
            var count = 0;
            if (this._currentConnection == null)
            {
                throw new InvalidOperationException("No active connection available for batch insert");
            }

            using (var cmd = this._currentConnection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Transaction = this._currentTransaction;
                var parameter = cmd.Parameters.Add("@data", DbType.String);

                foreach (var entity in entities)
                {
                    parameter.Value = JsonConvert.SerializeObject(entity);
                    cmd.ExecuteNonQuery();
                    count++;
                }
            }

            return count;
        }

        public bool Update(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var id = this._idProperty.GetValue(entity) ?? throw new InvalidOperationException("Entity must have an ID to update");
            var json = JsonConvert.SerializeObject(entity);
            var sql = $"UPDATE {this._tableName} SET Data = @data, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @id";

            var affected = this.ExecuteCommand(sql, new { data = json, id = id });
            this.logger?.LogDebug($"Updated entity with ID {id} in {this._tableName}");

            return affected > 0;
        }

        public bool Delete(long id)
        {
            var sql = $"DELETE FROM {this._tableName} WHERE Id = @id";
            var affected = this.ExecuteCommand(sql, new { id = id });

            this.logger?.LogDebug($"Deleted entity with ID {id} from {this._tableName}");
            return affected > 0;
        }

        public T GetById(long id)
        {
            var sql = $"SELECT Id, Data FROM {this._tableName} WHERE Id = @id";
            return this.ExecuteQuery(sql, new { id = id }).FirstOrDefault();
        }

        public IEnumerable<T> GetAll()
        {
            var sql = $"SELECT Id, Data FROM {this._tableName} ORDER BY Id";
            return this.ExecuteQuery(sql);
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            // For simplicity, load all and filter in memory
            // In production, consider expression tree parsing for SQL generation
            var all = this.GetAll();
            return all.Where(predicate.Compile());
        }

        public long Count()
        {
            var sql = $"SELECT COUNT(*) FROM {this._tableName}";

            var connection = this.GetConnection();
            var isTransactionConnection = connection == this._currentConnection;
            var shouldClose = !isTransactionConnection && connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Transaction = this._currentTransaction;
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
            finally
            {
                if (shouldClose && !isTransactionConnection)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        public long Count(Expression<Func<T, bool>> predicate)
        {
            // For simplicity, load all and count in memory
            return this.Find(predicate).Count();
        }

        public IEnumerable<T> ExecuteQuery(string sql, params object[] parameters)
        {
            var results = new List<T>();

            var connection = this.GetConnection();
            var isTransactionConnection = connection == this._currentConnection;
            var shouldClose = !isTransactionConnection && connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Transaction = this._currentTransaction;

                    this.AddParameters(cmd, parameters);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var json = reader.GetString(reader.GetOrdinal("Data"));
                            var entity = JsonConvert.DeserializeObject<T>(json);
                            if (entity == null)
                            {
                                continue;
                            }

                            if (reader.GetOrdinal("Id") >= 0)
                            {
                                var id = reader.GetInt64(reader.GetOrdinal("Id"));
                                this._idProperty.SetValue(entity, id);
                            }

                            results.Add(entity);
                        }
                    }
                }
            }
            finally
            {
                if (shouldClose && !isTransactionConnection)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }

            return results;
        }

        public int ExecuteCommand(string sql, params object[] parameters)
        {
            var connection = this.GetConnection();
            var isTransactionConnection = connection == this._currentConnection;
            var shouldClose = !isTransactionConnection && connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Transaction = this._currentTransaction;

                    this.AddParameters(cmd, parameters);

                    return cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (shouldClose && !isTransactionConnection)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        private void AddParameters(SQLiteCommand cmd, object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return;
            }

            foreach (var param in parameters)
            {
                if (param == null)
                {
                    continue;
                }

                var type = param.GetType();
                if (type.IsAnonymousType())
                {
                    foreach (var prop in type.GetProperties())
                    {
                        cmd.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(param) ?? DBNull.Value);
                    }
                }
                else
                {
                    cmd.Parameters.AddWithValue("@p" + cmd.Parameters.Count, param);
                }
            }
        }

        public IDisposable BeginTransaction()
        {
            if (this._currentTransaction != null)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }

            this._currentConnection = this.CreateConnection();
            this._currentConnection.Open();
            this._currentTransaction = this._currentConnection.BeginTransaction();

            return new TransactionScope(this);
        }

        // Transaction scope that provides implicit commit behavior for backward compatibility
        private class TransactionScope : IDisposable
        {
            private readonly PersistenceProvider<T> _provider;
            private readonly SQLiteTransaction _transaction;
            private readonly SQLiteConnection _connection;
            private bool _disposed;
            private Exception _firstException;

            public TransactionScope(PersistenceProvider<T> provider)
            {
                this._provider = provider;
                this._transaction = provider._currentTransaction;
                this._connection = provider._currentConnection;

                // Register for first chance exceptions to detect if any exception occurs
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
            }

            private void OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
            {
                // Capture the first exception that occurs while this transaction is active
                if (_firstException == null && !_disposed && _transaction != null)
                {
                    _firstException = e.Exception;
                }
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;

                this._disposed = true;

                // Unregister event handler
                AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;

                // Clear provider references first
                this._provider._currentTransaction = null;
                this._provider._currentConnection = null;

                if (_transaction != null)
                {
                    try
                    {
                        // Commit only if no exception was thrown during the transaction
                        if (_firstException == null)
                        {
                            _transaction.Commit();
                        }
                        else
                        {
                            _transaction.Rollback();
                        }
                    }
                    catch (Exception ex)
                    {
                        this._provider._logger?.LogError(ex, "Error during transaction finalization");

                        // Try to rollback if commit failed
                        try
                        {
                            _transaction.Rollback();
                        }
                        catch
                        {
                            // Ignore rollback errors
                        }

                        // Re-throw only if we were trying to commit
                        if (_firstException == null)
                            throw;
                    }
                    finally
                    {
                        // Always clean up
                        _transaction.Dispose();
                        _connection?.Close();
                        _connection?.Dispose();
                    }
                }
            }
        }


        public TResult ExecuteScalar<TResult>(string sql)
        {
            using (var connection = this.CreateConnection())
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return default(TResult);
                    return (TResult)Convert.ChangeType(result, typeof(TResult));
                }
            }
        }

        public void Vacuum()
        {
            this.ExecuteCommand("VACUUM");
            this.logger?.LogInformation($"Vacuum completed for {this._tableName}");
        }

        public void Analyze()
        {
            this.ExecuteCommand($"ANALYZE {this._tableName}");
            this.logger?.LogInformation($"Analyze completed for {this._tableName}");
        }

        public void EnsureTable<T, TKey>() where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            using var connection = this.CreateConnection();
            connection.Open();

            // Enable foreign keys
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }

            // Set journal mode to WAL for better concurrency
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL;";
                cmd.ExecuteNonQuery();
            }

            T entity = new T();
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {entity.TableName} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Data TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_{this._tableName}_created ON {this._tableName}(CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_{this._tableName}_updated ON {this._tableName}(UpdatedAt);";

            this.ExecuteCommand(sql);
            this.logger?.LogInformation($"Table {this._tableName} created or verified");
        }

        public T GetById<T, TKey>(TKey id) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetAll<T, TKey>() where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> Find<T, TKey>(Expression<Func<T, bool>> predicate) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public long Count<T, TKey>() where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public long Count<T, TKey>(Expression<Func<T, bool>> predicate) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public T Insert<T, TKey>(T entity) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public int InsertBatch<T, TKey>(IEnumerable<T> entities) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public bool Update<T, TKey>(T entity) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }

        public bool Delete<T, TKey>(TKey id) where T : class, IEntity<TKey>, new() where TKey : IEquatable<TKey>
        {
            throw new NotImplementedException();
        }
    }
}
