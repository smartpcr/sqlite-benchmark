using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SQLite.Lib.Abstractions;
using SQLite.Lib.Models;

namespace SQLite.Lib.Implementations
{
    /// <summary>
    /// Generic SQLite provider for CRUD operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public class SqliteProvider<T> : ISqliteProvider<T> where T : class, new()
    {
        private readonly string _connectionString;
        private readonly ILogger<SqliteProvider<T>> _logger;
        private readonly string _tableName;
        private readonly PropertyInfo _idProperty;
        private SQLiteConnection _currentConnection;
        private SQLiteTransaction _currentTransaction;

        public SqliteProvider(string connectionString, ILogger<SqliteProvider<T>> logger = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger;
            _tableName = typeof(T).Name;
            _idProperty = typeof(T).GetProperty("Id") ?? throw new InvalidOperationException($"Type {typeof(T).Name} must have an Id property");

            Initialize();
        }

        private void Initialize()
        {
            using (var connection = CreateConnection())
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
            return new SQLiteConnection(_connectionString);
        }

        private SQLiteConnection GetConnection()
        {
            return _currentConnection ?? CreateConnection();
        }

        public void CreateTable()
        {
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {_tableName} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Data TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_{_tableName}_created ON {_tableName}(CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_{_tableName}_updated ON {_tableName}(UpdatedAt);";

            ExecuteCommand(sql);
            _logger?.LogInformation($"Table {_tableName} created or verified");
        }

        public T Insert(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var json = JsonConvert.SerializeObject(entity);
            var sql = $"INSERT INTO {_tableName} (Data) VALUES (@data); SELECT last_insert_rowid();";

            using (var connection = GetConnection())
            {
                var shouldClose = connection.State != ConnectionState.Open;
                if (shouldClose) connection.Open();

                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue("@data", json);
                        cmd.Transaction = _currentTransaction;

                        var id = Convert.ToInt64(cmd.ExecuteScalar());
                        _idProperty.SetValue(entity, id);

                        _logger?.LogDebug($"Inserted entity with ID {id} into {_tableName}");
                        return entity;
                    }
                }
                finally
                {
                    if (shouldClose) connection.Close();
                }
            }
        }

        public int InsertBatch(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (!entityList.Any()) return 0;

            var count = 0;
            var wasInTransaction = _currentTransaction != null;

            if (!wasInTransaction)
            {
                using (BeginTransaction())
                {
                    count = InsertBatchInternal(entityList);
                }
            }
            else
            {
                count = InsertBatchInternal(entityList);
            }

            _logger?.LogInformation($"Batch inserted {count} entities into {_tableName}");
            return count;
        }

        private int InsertBatchInternal(List<T> entities)
        {
            var sql = $"INSERT INTO {_tableName} (Data) VALUES (@data)";
            var count = 0;

            using (var cmd = _currentConnection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Transaction = _currentTransaction;
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
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var id = _idProperty.GetValue(entity);
            if (id == null) throw new InvalidOperationException("Entity must have an ID to update");

            var json = JsonConvert.SerializeObject(entity);
            var sql = $"UPDATE {_tableName} SET Data = @data, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @id";

            var affected = ExecuteCommand(sql, new { data = json, id = id });
            _logger?.LogDebug($"Updated entity with ID {id} in {_tableName}");

            return affected > 0;
        }

        public bool Delete(long id)
        {
            var sql = $"DELETE FROM {_tableName} WHERE Id = @id";
            var affected = ExecuteCommand(sql, new { id = id });

            _logger?.LogDebug($"Deleted entity with ID {id} from {_tableName}");
            return affected > 0;
        }

        public T GetById(long id)
        {
            var sql = $"SELECT Id, Data FROM {_tableName} WHERE Id = @id";
            return ExecuteQuery(sql, new { id = id }).FirstOrDefault();
        }

        public IEnumerable<T> GetAll()
        {
            var sql = $"SELECT Id, Data FROM {_tableName} ORDER BY Id";
            return ExecuteQuery(sql);
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            // For simplicity, load all and filter in memory
            // In production, consider expression tree parsing for SQL generation
            var all = GetAll();
            return all.Where(predicate.Compile());
        }

        public long Count()
        {
            var sql = $"SELECT COUNT(*) FROM {_tableName}";

            using (var connection = GetConnection())
            {
                var shouldClose = connection.State != ConnectionState.Open;
                if (shouldClose) connection.Open();

                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Transaction = _currentTransaction;
                        return Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }
                finally
                {
                    if (shouldClose) connection.Close();
                }
            }
        }

        public long Count(Expression<Func<T, bool>> predicate)
        {
            // For simplicity, load all and count in memory
            return Find(predicate).Count();
        }

        public IEnumerable<T> ExecuteQuery(string sql, params object[] parameters)
        {
            var results = new List<T>();

            using (var connection = GetConnection())
            {
                var shouldClose = connection.State != ConnectionState.Open;
                if (shouldClose) connection.Open();

                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Transaction = _currentTransaction;

                        AddParameters(cmd, parameters);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var json = reader.GetString(reader.GetOrdinal("Data"));
                                var entity = JsonConvert.DeserializeObject<T>(json);

                                if (reader.GetOrdinal("Id") >= 0)
                                {
                                    var id = reader.GetInt64(reader.GetOrdinal("Id"));
                                    _idProperty.SetValue(entity, id);
                                }

                                results.Add(entity);
                            }
                        }
                    }
                }
                finally
                {
                    if (shouldClose) connection.Close();
                }
            }

            return results;
        }

        public int ExecuteCommand(string sql, params object[] parameters)
        {
            using (var connection = GetConnection())
            {
                var shouldClose = connection.State != ConnectionState.Open;
                if (shouldClose) connection.Open();

                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Transaction = _currentTransaction;

                        AddParameters(cmd, parameters);

                        return cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    if (shouldClose) connection.Close();
                }
            }
        }

        private void AddParameters(SQLiteCommand cmd, object[] parameters)
        {
            if (parameters == null || parameters.Length == 0) return;

            foreach (var param in parameters)
            {
                if (param == null) continue;

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
            if (_currentTransaction != null)
                throw new InvalidOperationException("Transaction already in progress");

            _currentConnection = CreateConnection();
            _currentConnection.Open();
            _currentTransaction = _currentConnection.BeginTransaction();

            return new TransactionScope(this);
        }

        private class TransactionScope : IDisposable
        {
            private readonly SqliteProvider<T> _provider;
            private bool _disposed;

            public TransactionScope(SqliteProvider<T> provider)
            {
                _provider = provider;
            }

            public void Dispose()
            {
                if (_disposed) return;

                try
                {
                    _provider._currentTransaction?.Commit();
                }
                catch
                {
                    _provider._currentTransaction?.Rollback();
                    throw;
                }
                finally
                {
                    _provider._currentTransaction?.Dispose();
                    _provider._currentConnection?.Close();
                    _provider._currentConnection?.Dispose();
                    _provider._currentTransaction = null;
                    _provider._currentConnection = null;
                    _disposed = true;
                }
            }
        }

        public void Vacuum()
        {
            ExecuteCommand("VACUUM");
            _logger?.LogInformation($"Vacuum completed for {_tableName}");
        }

        public void Analyze()
        {
            ExecuteCommand($"ANALYZE {_tableName}");
            _logger?.LogInformation($"Analyze completed for {_tableName}");
        }
    }

    internal static class TypeExtensions
    {
        public static bool IsAnonymousType(this Type type)
        {
            return type.IsClass
                && type.IsSealed
                && type.Attributes.HasFlag(TypeAttributes.NotPublic)
                && type.Name.StartsWith("<>")
                && type.Name.Contains("AnonymousType");
        }
    }
}
