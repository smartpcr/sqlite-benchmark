// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Serialization;

    /// <summary>
    /// Base mapper that uses reflection and attributes to create mappings between C# properties and database columns.
    /// </summary>
    /// <typeparam name="T">The entity type to map</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    public class BaseEntityMapper<T, TKey> : ISQLiteEntityMapper<T, TKey> where T : class, IEntity<TKey> where TKey : IEquatable<TKey>
    {
        #region Private Fields

        private readonly Type entityType;
        private readonly string tableName;
        private readonly string schemaName;
        private readonly Dictionary<PropertyInfo, PropertyMapping> propertyMappings;
        private readonly List<IndexDefinition> indexes;
        private readonly List<ForeignKeyDefinition> foreignKeys;
        private PropertyInfo primaryKeyProperty;
        private bool hasCompositeKey;
        private readonly List<PropertyInfo> compositeKeyProperties;
        private readonly ISerializer<T> serializer;

        #endregion

        #region Constructor

        public BaseEntityMapper()
        {
            this.entityType = typeof(T);
            this.propertyMappings = new Dictionary<PropertyInfo, PropertyMapping>();
            this.indexes = new List<IndexDefinition>();
            this.foreignKeys = new List<ForeignKeyDefinition>();
            this.compositeKeyProperties = new List<PropertyInfo>();

            // Extract table information
            var tableAttr = this.entityType.GetCustomAttribute<TableAttribute>();
            this.tableName = tableAttr?.Name ?? this.entityType.Name;
            this.schemaName = tableAttr?.Schema ?? "dbo";
            this.serializer = SerializerResolver.GetSerializer<T>();

            // Build property mappings
            this.BuildPropertyMappings();

            // Validate primary key
            if (this.primaryKeyProperty == null && !this.hasCompositeKey)
            {
                throw new InvalidOperationException(
                    $"Entity type {this.entityType.Name} must have at least one property marked with [PrimaryKey] or properties named 'Id' or 'Key'");
            }
        }

        #endregion

        #region Public Methods - Table Information

        /// <summary>
        /// Gets the fully qualified table name including schema.
        /// </summary>
        public virtual string GetFullTableName() => $"{this.schemaName}.{this.tableName}";

        /// <summary>
        /// Gets the table name without schema.
        /// </summary>
        public virtual string GetTableName() => this.tableName;

        #endregion

        #region Public Methods - Mapping Information

        /// <summary>
        /// Gets all property mappings.
        /// </summary>
        public IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetPropertyMappings() => this.propertyMappings;

        /// <summary>
        /// Gets the primary key column name.
        /// </summary>
        public string GetPrimaryKeyColumn()
        {
            return this.propertyMappings.FirstOrDefault(m => m.Value.IsPrimaryKey).Value?.ColumnName;
        }

        /// <summary>
        /// Gets the primary key property mapping.
        /// </summary>
        public PropertyMapping GetPrimaryKeyMapping()
        {
            if (this.hasCompositeKey)
            {
                throw new InvalidOperationException("Entity has composite key. Use GetCompositeKeyMappings() instead.");
            }
            return this.propertyMappings[this.primaryKeyProperty];
        }

        /// <summary>
        /// Gets composite key property mappings if the entity has a composite primary key.
        /// </summary>
        public IEnumerable<PropertyMapping> GetCompositeKeyMappings()
        {
            if (!this.hasCompositeKey)
            {
                throw new InvalidOperationException("Entity has single primary key. Use GetPrimaryKeyMapping() instead.");
            }
            return this.compositeKeyProperties.Select(p => this.propertyMappings[p]);
        }

        #endregion

        #region Public Methods - SQL Generation

        /// <summary>
        /// Generates CREATE TABLE SQL statement for the entity.
        /// </summary>
        public virtual string GenerateCreateTableSql(bool includeIfNotExists = true)
        {
            var sql = new StringBuilder();

            if (includeIfNotExists)
            {
                sql.AppendLine($"CREATE TABLE IF NOT EXISTS {this.GetFullTableName()} (");
            }
            else
            {
                sql.AppendLine($"CREATE TABLE {this.GetFullTableName()} (");
            }

            // Add column definitions
            var columnDefinitions = new List<string>();
            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                columnDefinitions.Add(this.GenerateColumnDefinition(mapping));
            }

            // Add primary key constraint
            if (this.hasCompositeKey)
            {
                var keyColumns = string.Join(", ", this.compositeKeyProperties
                    .Select(p => this.propertyMappings[p].ColumnName));
                columnDefinitions.Add($"PRIMARY KEY ({keyColumns})");
            }

            // Add foreign key constraints
            foreach (var fk in this.foreignKeys)
            {
                columnDefinitions.Add(this.GenerateForeignKeyConstraint(fk));
            }

            sql.AppendLine(string.Join(",\n", columnDefinitions.Select(d => $"    {d}")));
            sql.AppendLine(");");

            return sql.ToString();
        }

        /// <summary>
        /// Generates CREATE INDEX SQL statements for the entity.
        /// </summary>
        public virtual IEnumerable<string> GenerateCreateIndexSql()
        {
            var indexSql = new List<string>();

            foreach (var index in this.indexes)
            {
                var sql = new StringBuilder();
                sql.Append("CREATE ");

                if (index.IsUnique)
                    sql.Append("UNIQUE ");

                sql.Append($"INDEX IF NOT EXISTS {index.Name} ");
                sql.Append($"ON {this.GetFullTableName()} (");
                sql.Append(string.Join(", ", index.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName)));
                sql.Append(");");

                indexSql.Add(sql.ToString());
            }

            return indexSql;
        }

        public string GenerateWhereClause(Expression<Func<T, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Public Methods - Column Selection

        /// <summary>
        /// Gets the columns to select in SELECT queries.
        /// </summary>
        public virtual List<string> GetSelectColumns()
        {
            return this.propertyMappings.Values
                .Where(m => !m.IsNotMapped)
                .Select(m => m.ColumnName)
                .ToList();
        }

        /// <summary>
        /// Gets the columns to include in INSERT statements.
        /// </summary>
        public virtual List<string> GetInsertColumns()
        {
            return this.propertyMappings.Values
                .Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsAutoIncrement)
                .Select(m => m.ColumnName)
                .ToList();
        }

        /// <summary>
        /// Gets the columns to include in UPDATE statements.
        /// </summary>
        public virtual List<string> GetUpdateColumns()
        {
            return this.propertyMappings.Values
                .Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsPrimaryKey)
                .Select(m => m.ColumnName)
                .ToList();
        }

        #endregion

        #region Public Methods - Parameter Handling

        /// <summary>
        /// Adds parameters to a SQLite command for the given entity.
        /// </summary>
        public void AddParameters(SQLiteCommand command, T entity)
        {
            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds parameters to a command for the given entity.
        /// </summary>
        public virtual void AddParameters(System.Data.Common.DbCommand command, T entity)
        {
            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        #endregion

        #region Public Methods - Data Mapping

        /// <summary>
        /// Maps a data reader row to an entity instance.
        /// Supports entities with parameterized constructors by collecting property values
        /// and using reflection to find and invoke the appropriate constructor.
        /// </summary>
        public virtual T MapFromReader(IDataReader reader)
        {
            // Step 1: Collect all property values from the reader into a dictionary
            var propertyValues = new Dictionary<string, object>();

            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                try
                {
                    var ordinal = reader.GetOrdinal(mapping.ColumnName);
                    if (!reader.IsDBNull(ordinal))
                    {
                        var dbValue = reader.GetValue(ordinal);
                        // Convert database value to appropriate C# type
                        var convertedValue = this.ConvertDbValueToCSharpType(dbValue, mapping.PropertyType);
                        propertyValues[mapping.PropertyName] = convertedValue;
                    }
                    else
                    {
                        propertyValues[mapping.PropertyName] = null;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // Column not in result set, skip
                    propertyValues[mapping.PropertyName] = null;
                }
            }

            // Step 2: Try to create instance using constructor mapping
            var entity = this.CreateInstanceWithConstructor(propertyValues);

            // Step 3: Set any remaining properties that weren't set by constructor
            this.SetRemainingProperties(entity, propertyValues);

            return entity;
        }

        #endregion

        #region Public Methods - Serialization

        /// <summary>
        /// Serializes an entity to a byte array.
        /// </summary>
        public byte[] SerializeEntity(T entity)
        {
            return this.serializer.Serialize(entity);
        }

        /// <summary>
        /// Serializes a key value to a string representation for database storage.
        /// </summary>
        public virtual string SerializeKey(TKey key)
        {
            if (key == null)
                return null;

            var keyType = typeof(TKey);
            var underlyingType = Nullable.GetUnderlyingType(keyType) ?? keyType;

            // If TKey is string, return as-is
            if (underlyingType == typeof(string))
            {
                return key.ToString();
            }
            // For numeric types, use ToString()
            else if (underlyingType == typeof(int) ||
                     underlyingType == typeof(long) ||
                     underlyingType == typeof(short) ||
                     underlyingType == typeof(byte) ||
                     underlyingType == typeof(decimal) ||
                     underlyingType == typeof(double) ||
                     underlyingType == typeof(float))
            {
                return key.ToString();
            }
            // For DateTime types, use ISO 8601 format for SQLite compatibility
            else if (underlyingType == typeof(DateTime))
            {
                return ((DateTime)(object)key).ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                return ((DateTimeOffset)(object)key).ToString("yyyy-MM-dd HH:mm:ss.fffzzz");
            }
            // For boolean values
            else if (underlyingType == typeof(bool))
            {
                return ((bool)(object)key) ? "1" : "0";
            }
            // For enum values, use the underlying integer value
            else if (underlyingType.IsEnum)
            {
                return Convert.ToInt32(key).ToString();
            }
            // For Guid values
            else if (underlyingType == typeof(Guid))
            {
                return ((Guid)(object)key).ToString();
            }
            // For other types, fall back to ToString()
            else
            {
                return key.ToString();
            }
        }

        /// <summary>
        /// Deserializes a string representation back to the original key type.
        /// </summary>
        public virtual TKey DeserializeKey(string serialized)
        {
            if (string.IsNullOrEmpty(serialized))
                return default(TKey);

            var keyType = typeof(TKey);
            var underlyingType = Nullable.GetUnderlyingType(keyType) ?? keyType;

            try
            {
                // If TKey is string, return as-is
                if (underlyingType == typeof(string))
                {
                    return (TKey)(object)serialized;
                }
                // For numeric types, parse appropriately
                else if (underlyingType == typeof(int))
                {
                    return (TKey)(object)int.Parse(serialized);
                }
                else if (underlyingType == typeof(long))
                {
                    return (TKey)(object)long.Parse(serialized);
                }
                else if (underlyingType == typeof(short))
                {
                    return (TKey)(object)short.Parse(serialized);
                }
                else if (underlyingType == typeof(byte))
                {
                    return (TKey)(object)byte.Parse(serialized);
                }
                else if (underlyingType == typeof(decimal))
                {
                    return (TKey)(object)decimal.Parse(serialized);
                }
                else if (underlyingType == typeof(double))
                {
                    return (TKey)(object)double.Parse(serialized);
                }
                else if (underlyingType == typeof(float))
                {
                    return (TKey)(object)float.Parse(serialized);
                }
                // For DateTime types, parse from ISO 8601 format
                else if (underlyingType == typeof(DateTime))
                {
                    return (TKey)(object)DateTime.Parse(serialized);
                }
                else if (underlyingType == typeof(DateTimeOffset))
                {
                    return (TKey)(object)DateTimeOffset.Parse(serialized);
                }
                // For boolean values
                else if (underlyingType == typeof(bool))
                {
                    // Handle both "1"/"0" and "true"/"false" formats
                    if (serialized == "1" || string.Equals(serialized, "true", StringComparison.OrdinalIgnoreCase))
                        return (TKey)(object)true;
                    else if (serialized == "0" || string.Equals(serialized, "false", StringComparison.OrdinalIgnoreCase))
                        return (TKey)(object)false;
                    else
                        return (TKey)(object)bool.Parse(serialized);
                }
                // For enum values, parse from integer representation
                else if (underlyingType.IsEnum)
                {
                    var enumValue = int.Parse(serialized);
                    return (TKey)Enum.ToObject(underlyingType, enumValue);
                }
                // For Guid values
                else if (underlyingType == typeof(Guid))
                {
                    return (TKey)(object)Guid.Parse(serialized);
                }
                // For other types, try Convert.ChangeType as fallback
                else
                {
                    return (TKey)Convert.ChangeType(serialized, underlyingType);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to deserialize key value '{serialized}' to type {keyType.Name}. " +
                    $"Original error: {ex.Message}", ex);
            }
        }

        #endregion

        #region Public Methods - Command Creation

        public SQLiteCommand CreateCommand(DbOperationType operationType, TKey key, T fromValue, T toValue)
        {
            var command = new SQLiteCommand();

            switch (operationType)
            {
                case DbOperationType.Select:
                    command.CommandText = this.GenerateSelectCommand();
                    this.AddSelectParameters(command, key);
                    break;

                case DbOperationType.Insert:
                    command.CommandText = this.GenerateInsertCommand();
                    this.AddParameters(command, fromValue);
                    break;

                case DbOperationType.Update:
                    command.CommandText = this.GenerateUpdateCommand();
                    this.AddUpdateParameters(command, fromValue, toValue);
                    break;

                case DbOperationType.Delete:
                    command.CommandText = this.GenerateDeleteCommand();
                    this.AddDeleteParameters(command, fromValue);
                    break;

                default:
                    throw new ArgumentException($"Unsupported operation type: {operationType}");
            }

            return command;
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Converts a database value to the appropriate C# type.
        /// </summary>
        protected virtual object ConvertDbValueToCSharpType(object dbValue, Type targetType)
        {
            if (dbValue == null || dbValue == DBNull.Value)
                return null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Handle common type conversions
            if (underlyingType == typeof(string))
            {
                return dbValue.ToString();
            }
            else if (underlyingType == typeof(int))
            {
                return Convert.ToInt32(dbValue);
            }
            else if (underlyingType == typeof(long))
            {
                return Convert.ToInt64(dbValue);
            }
            else if (underlyingType == typeof(short))
            {
                return Convert.ToInt16(dbValue);
            }
            else if (underlyingType == typeof(byte))
            {
                return Convert.ToByte(dbValue);
            }
            else if (underlyingType == typeof(bool))
            {
                // SQLite stores booleans as integers
                return Convert.ToBoolean(dbValue);
            }
            else if (underlyingType == typeof(decimal))
            {
                return Convert.ToDecimal(dbValue);
            }
            else if (underlyingType == typeof(double))
            {
                return Convert.ToDouble(dbValue);
            }
            else if (underlyingType == typeof(float))
            {
                return Convert.ToSingle(dbValue);
            }
            else if (underlyingType == typeof(DateTime))
            {
                if (dbValue is string dateStr)
                {
                    return DateTime.Parse(dateStr);
                }
                return Convert.ToDateTime(dbValue);
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                if (dbValue is string dateOffsetStr)
                {
                    return DateTimeOffset.Parse(dateOffsetStr);
                }
                else if (dbValue is long unixTime)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime);
                }
                return (DateTimeOffset)dbValue;
            }
            else if (underlyingType == typeof(TimeSpan))
            {
                if (dbValue is string timeStr)
                {
                    return TimeSpan.Parse(timeStr);
                }
                return (TimeSpan)dbValue;
            }
            else if (underlyingType == typeof(Guid))
            {
                if (dbValue is string guidStr)
                {
                    return Guid.Parse(guidStr);
                }
                return (Guid)dbValue;
            }
            else if (underlyingType.IsEnum)
            {
                return Enum.ToObject(underlyingType, dbValue);
            }
            else if (underlyingType == typeof(byte[]))
            {
                return (byte[])dbValue;
            }

            // For complex types, try direct conversion
            try
            {
                return Convert.ChangeType(dbValue, underlyingType);
            }
            catch
            {
                // If conversion fails, return the raw value
                return dbValue;
            }
        }

        /// <summary>
        /// Creates an instance of T using the most appropriate constructor based on available property values.
        /// </summary>
        protected virtual T CreateInstanceWithConstructor(Dictionary<string, object> propertyValues)
        {
            // First, look for a constructor marked with JsonConstructor attribute
            var jsonConstructor = typeof(T).GetConstructors()
                .FirstOrDefault(c => c.GetCustomAttribute<System.Text.Json.Serialization.JsonConstructorAttribute>() != null ||
                                   c.GetCustomAttribute<Newtonsoft.Json.JsonConstructorAttribute>() != null);

            if (jsonConstructor != null)
            {
                // Try to use the JsonConstructor first
                if (this.TryInvokeConstructor(jsonConstructor, propertyValues, out T result))
                {
                    return result;
                }
            }

            // Fallback: try other constructors in order of parameter count
            var constructors = typeof(T).GetConstructors()
                .Where(c => c != jsonConstructor) // Exclude the JsonConstructor we already tried
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            foreach (var constructor in constructors)
            {
                if (this.TryInvokeConstructor(constructor, propertyValues, out T result))
                {
                    return result;
                }
            }

            // Fallback: try parameterless constructor
            try
            {
                return Activator.CreateInstance<T>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Unable to create instance of type {typeof(T).Name}. " +
                    "No suitable constructor found that matches the available property values, " +
                    "and no parameterless constructor is available.");
            }
        }

        /// <summary>
        /// Attempts to invoke a constructor with the given property values.
        /// </summary>
        protected virtual bool TryInvokeConstructor(ConstructorInfo constructor, Dictionary<string, object> propertyValues, out T result)
        {
            result = default(T);
            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];
            bool canUseConstructor = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var parameterName = this.GetParameterMappingName(param);

                if (propertyValues.ContainsKey(parameterName) && propertyValues[parameterName] != null)
                {
                    try
                    {
                        // Convert the value to the parameter type if needed
                        args[i] = this.ConvertValueToParameterType(propertyValues[parameterName], param.ParameterType);
                    }
                    catch
                    {
                        // If conversion fails, check if parameter has default value
                        if (param.HasDefaultValue)
                        {
                            args[i] = param.DefaultValue;
                        }
                        else
                        {
                            canUseConstructor = false;
                            break;
                        }
                    }
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else if (param.ParameterType.IsValueType)
                {
                    args[i] = Activator.CreateInstance(param.ParameterType);
                }
                else
                {
                    args[i] = null;
                }
            }

            if (canUseConstructor)
            {
                try
                {
                    result = (T)constructor.Invoke(args);
                    return true;
                }
                catch
                {
                    // Constructor invocation failed
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the property name that a constructor parameter maps to.
        /// For constructors marked with JsonConstructor, uses standard property name matching.
        /// </summary>
        protected virtual string GetParameterMappingName(ParameterInfo parameter)
        {
            // Check if the constructor is marked with JsonConstructor
            var constructor = parameter.Member as ConstructorInfo;
            bool isJsonConstructor = constructor?.GetCustomAttribute<System.Text.Json.Serialization.JsonConstructorAttribute>() != null ||
                                   constructor?.GetCustomAttribute<Newtonsoft.Json.JsonConstructorAttribute>() != null;

            if (isJsonConstructor)
            {
                // For JsonConstructor, we rely on parameter name matching to properties
                // Look for a property that matches the parameter name (case-insensitive)
                var matchingProperty = typeof(T).GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

                if (matchingProperty != null)
                {
                    return matchingProperty.Name;
                }

                // If no exact match, try with proper casing (parameter name -> Property name)
                return char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
            }

            // For non-JsonConstructor constructors, use the original logic
            // Check for JsonPropertyName attribute on the parameter (legacy support)
            var jsonPropertyAttr = parameter.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
            if (jsonPropertyAttr != null)
            {
                return jsonPropertyAttr.Name;
            }

            // Check for Newtonsoft.Json JsonProperty attribute (legacy support)
            var newtonsoftJsonAttr = parameter.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
            if (newtonsoftJsonAttr != null && !string.IsNullOrEmpty(newtonsoftJsonAttr.PropertyName))
            {
                return newtonsoftJsonAttr.PropertyName;
            }

            // Check if there's a matching property
            var matchingProp = typeof(T).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

            if (matchingProp != null)
            {
                return matchingProp.Name;
            }

            // Default: use parameter name with proper casing
            return char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
        }

        /// <summary>
        /// Converts a value to the specified parameter type.
        /// </summary>
        protected virtual object ConvertValueToParameterType(object value, Type parameterType)
        {
            if (value == null)
                return null;

            if (parameterType.IsAssignableFrom(value.GetType()))
                return value;

            var underlyingType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            return Convert.ChangeType(value, underlyingType);
        }

        /// <summary>
        /// Sets any properties that weren't set by the constructor.
        /// </summary>
        protected virtual void SetRemainingProperties(T entity, Dictionary<string, object> propertyValues)
        {
            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                if (propertyValues.ContainsKey(mapping.PropertyName))
                {
                    try
                    {
                        var currentValue = mapping.PropertyInfo.GetValue(entity);
                        var newValue = propertyValues[mapping.PropertyName];

                        // Only set if the property wasn't already set by constructor
                        // or if the current value is the default value
                        if (currentValue == null ||
                            (currentValue.Equals(this.GetDefaultValue(mapping.PropertyType)) && newValue != null))
                        {
                            var convertedValue = this.ConvertValueToParameterType(newValue, mapping.PropertyType);
                            mapping.PropertyInfo.SetValue(entity, convertedValue);
                        }
                    }
                    catch
                    {
                        // Property setting failed, skip
                    }
                }
            }
        }

        /// <summary>
        /// Gets the default value for a type.
        /// </summary>
        protected virtual object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        #endregion

        #region Private Methods - SQL Generation

        /// <summary>
        /// Generates SELECT command SQL.
        /// </summary>
        private string GenerateSelectCommand()
        {
            var tableName = this.GetTableName();
            var selectColumns = string.Join(", ", this.GetSelectColumns());
            var primaryKeyColumns = this.GetPrimaryKeyColumns()
                .Where(pk => !pk.Equals("Version", StringComparison.OrdinalIgnoreCase));
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{col} = @{col}"));
            var orderClause = this.propertyMappings.Values.FirstOrDefault(p =>
                !p.IsNotMapped && !p.IsComputed &&
                p.ColumnName.Equals("Version", StringComparison.OrdinalIgnoreCase)) != null
                ? "ORDER BY Version DESC LIMIT 1"
                : string.Empty;

            return $"SELECT {selectColumns} FROM {tableName} WHERE {whereClause} {orderClause}";
        }

        /// <summary>
        /// Generates INSERT command SQL.
        /// </summary>
        private string GenerateInsertCommand()
        {
            var tableName = this.GetTableName();
            var insertColumns = this.GetInsertColumns();
            var columnNames = string.Join(", ", insertColumns);
            var parameterNames = string.Join(", ", insertColumns.Select(col => $"@{col}"));

            return $"INSERT INTO {tableName} ({columnNames}) VALUES ({parameterNames})";
        }

        /// <summary>
        /// Generates UPDATE command SQL.
        /// </summary>
        private string GenerateUpdateCommand()
        {
            var tableName = this.GetTableName();
            var updateColumns = this.GetUpdateColumns();
            var primaryKeyColumns = this.GetPrimaryKeyColumns();

            var setClause = string.Join(", ", updateColumns.Select(col => $"{col} = @{col}"));
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{col} = @old_{col}"));

            return $"UPDATE {tableName} SET {setClause} WHERE {whereClause}";
        }

        /// <summary>
        /// Generates DELETE command SQL.
        /// </summary>
        private string GenerateDeleteCommand()
        {
            var tableName = this.GetTableName();
            var primaryKeyColumns = this.GetPrimaryKeyColumns();
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{col} = @{col}"));

            return $"DELETE FROM {tableName} WHERE {whereClause}";
        }

        /// <summary>
        /// Adds parameters for SELECT operation.
        /// </summary>
        private void AddSelectParameters(SQLiteCommand command, TKey entityKey)
        {
            var primaryKeyMappings = this.propertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder);

            foreach (var mapping in primaryKeyMappings)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = entityKey;
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds parameters for UPDATE operation.
        /// </summary>
        private void AddUpdateParameters(SQLiteCommand command, T fromValue, T toValue)
        {
            // Add parameters for the SET clause (new values)
            foreach (var mapping in this.propertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsPrimaryKey))
            {
                var value = mapping.PropertyInfo.GetValue(toValue);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            // Add parameters for the WHERE clause (old primary key values)
            var primaryKeyMappings = this.propertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder);

            foreach (var mapping in primaryKeyMappings)
            {
                var value = mapping.PropertyInfo.GetValue(fromValue);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@old_{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds parameters for DELETE operation.
        /// </summary>
        private void AddDeleteParameters(SQLiteCommand command, T entity)
        {
            var primaryKeyMappings = this.propertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder);

            foreach (var mapping in primaryKeyMappings)
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Gets the primary key column names.
        /// </summary>
        private List<string> GetPrimaryKeyColumns()
        {
            return this.propertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder)
                .Select(m => m.ColumnName)
                .ToList();
        }

        #endregion

        #region Private Methods

        private void BuildPropertyMappings()
        {
            var properties = this.entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var indexGroups = new Dictionary<string, List<IndexColumn>>();
            var foreignKeyGroups = new Dictionary<string, List<(PropertyInfo Property, ForeignKeyAttribute Attribute, string ColumnName)>>();

            foreach (var property in properties)
            {
                // Check if property should be excluded
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                {
                    continue;
                }

                // Create property mapping
                var mapping = this.CreatePropertyMapping(property);
                this.propertyMappings[property] = mapping;

                // Check if this is a primary key
                var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
                if (pkAttr != null)
                {
                    if (pkAttr.IsComposite)
                    {
                        this.hasCompositeKey = true;
                        this.compositeKeyProperties.Add(property);
                    }
                    else
                    {
                        this.primaryKeyProperty = property;
                    }
                }
                else if (this.primaryKeyProperty == null && !this.hasCompositeKey &&
                         (property.Name == "Id" || property.Name == "Key"))
                {
                    // Convention-based primary key
                    this.primaryKeyProperty = property;
                    mapping.IsPrimaryKey = true;
                }

                // Process indexes
                var indexAttrs = property.GetCustomAttributes<IndexAttribute>();
                foreach (var indexAttr in indexAttrs)
                {
                    var indexName = indexAttr.Name ?? $"IX_{this.tableName}_{mapping.ColumnName}";

                    if (!indexGroups.ContainsKey(indexName))
                    {
                        indexGroups[indexName] = new List<IndexColumn>();
                    }

                    indexGroups[indexName].Add(new IndexColumn
                    {
                        ColumnName = mapping.ColumnName,
                        Order = indexAttr.Order,
                        IsIncluded = indexAttr.IsIncluded
                    });
                }

                // Process foreign keys
                var fkAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                if (fkAttr != null)
                {
                    var constraintName = fkAttr.Name ?? $"FK_{this.tableName}_{property.Name}";

                    if (!foreignKeyGroups.ContainsKey(constraintName))
                    {
                        foreignKeyGroups[constraintName] = new List<(PropertyInfo, ForeignKeyAttribute, string)>();
                    }

                    foreignKeyGroups[constraintName].Add((property, fkAttr, mapping.ColumnName));
                }
            }

            // Build foreign key definitions from grouped columns
            foreach (var fkGroup in foreignKeyGroups)
            {
                var orderedItems = fkGroup.Value.OrderBy(x => x.Attribute.Ordinal).ToList();
                var firstAttr = orderedItems.First().Attribute;

                if (orderedItems.Count > 1)
                {
                    // Composite foreign key - validate all attributes have same table and actions
                    var allSameTable = orderedItems.All(x => x.Attribute.ReferencedTable == firstAttr.ReferencedTable);
                    var allSameDelete = orderedItems.All(x => x.Attribute.OnDelete == firstAttr.OnDelete);
                    var allSameUpdate = orderedItems.All(x => x.Attribute.OnUpdate == firstAttr.OnUpdate);

                    if (!allSameTable || !allSameDelete || !allSameUpdate)
                    {
                        throw new InvalidOperationException(
                            $"Composite foreign key '{fkGroup.Key}' has inconsistent attributes. " +
                            "All properties must reference the same table and have the same ON DELETE/UPDATE actions.");
                    }

                    // Build arrays of columns and referenced columns in ordinal order
                    var columns = orderedItems.Select(x => x.ColumnName).ToArray();
                    var referencedColumns = orderedItems.Select(x => x.Attribute.ReferencedColumn).ToArray();

                    this.foreignKeys.Add(new ForeignKeyDefinition
                    {
                        ConstraintName = fkGroup.Key,
                        ColumnNames = columns,
                        ReferencedTable = firstAttr.ReferencedTable,
                        ReferencedColumns = referencedColumns,
                        OnDelete = firstAttr.OnDelete,
                        OnUpdate = firstAttr.OnUpdate
                    });
                }
                else
                {
                    // Single column foreign key
                    var item = orderedItems.First();
                    this.foreignKeys.Add(new ForeignKeyDefinition
                    {
                        ConstraintName = fkGroup.Key,
                        ColumnName = item.ColumnName,
                        ReferencedTable = item.Attribute.ReferencedTable,
                        ReferencedColumn = item.Attribute.ReferencedColumn,
                        OnDelete = item.Attribute.OnDelete,
                        OnUpdate = item.Attribute.OnUpdate
                    });
                }
            }

            // Build index definitions from grouped columns
            foreach (var group in indexGroups)
            {
                var firstColumn = group.Value.First();
                var firstIndexAttr = properties
                    .SelectMany(p => p.GetCustomAttributes<IndexAttribute>()
                        .Where(a => (a.Name ?? $"IX_{this.tableName}_{this.propertyMappings[p].ColumnName}") == group.Key))
                    .First();

                this.indexes.Add(new IndexDefinition
                {
                    Name = group.Key,
                    Columns = group.Value,
                    IsUnique = firstIndexAttr.IsUnique,
                    IsClustered = firstIndexAttr.IsClustered,
                    Filter = firstIndexAttr.Filter
                });
            }
        }

        private PropertyMapping CreatePropertyMapping(PropertyInfo property)
        {
            var mapping = new PropertyMapping
            {
                PropertyInfo = property,
                PropertyName = property.Name,
                PropertyType = property.PropertyType,
                IsNotMapped = false
            };

            // Get column attribute
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();

            // Column name
            mapping.ColumnName = columnAttr?.Name ?? property.Name;

            // Data type
            if (columnAttr?.SQLiteType != null)
            {
                mapping.SQLiteType = columnAttr.SQLiteType.Value;
                mapping.Size = columnAttr.Size;
            }
            else if (columnAttr?.SqlType != null)
            {
                mapping.SqlType = columnAttr.SqlType.Value;
                mapping.Size = columnAttr.Size;
                mapping.Precision = columnAttr.Precision;
                mapping.Scale = columnAttr.Scale;
            }
            else
            {
                // Infer SQL type from property type
                this.InferSqlType(property.PropertyType, mapping);
            }

            // Nullability - Use NotNull property (inverted logic)
            mapping.IsNullable = columnAttr != null ? !columnAttr.NotNull : this.IsNullableType(property.PropertyType);

            // Default value
            mapping.DefaultValue = columnAttr?.DefaultValue;
            mapping.DefaultConstraintName = columnAttr?.DefaultConstraintName;

            // Check constraint
            var checkAttr = property.GetCustomAttribute<CheckAttribute>();
            if (checkAttr != null)
            {
                mapping.CheckConstraint = checkAttr.Expression;
                mapping.CheckConstraintName = checkAttr.Name ?? $"CK_{this.tableName}_{mapping.ColumnName}";
            }

            // Computed column
            var computedAttr = property.GetCustomAttribute<ComputedAttribute>();
            if (computedAttr != null)
            {
                mapping.IsComputed = true;
                mapping.ComputedExpression = computedAttr.Expression;
                mapping.IsPersisted = computedAttr.IsPersisted;
            }

            // Audit fields
            var auditAttr = property.GetCustomAttribute<AuditFieldAttribute>();
            if (auditAttr != null)
            {
                mapping.IsAuditField = true;
                mapping.AuditFieldType = auditAttr.FieldType;
            }

            // Primary key
            var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
            if (pkAttr != null)
            {
                mapping.IsPrimaryKey = true;
                mapping.PrimaryKeyOrder = pkAttr.Order;
                mapping.IsAutoIncrement = pkAttr.IsAutoIncrement;
                mapping.SequenceName = pkAttr.SequenceName;
            }

            // Unique constraint
            var uniqueAttr = property.GetCustomAttribute<UniqueAttribute>();
            if (uniqueAttr != null)
            {
                mapping.IsUnique = true;
                mapping.UniqueConstraintName = uniqueAttr.Name ?? $"UQ_{this.tableName}_{mapping.ColumnName}";
            }

            return mapping;
        }

        private void InferSqlType(Type clrType, PropertyMapping mapping)
        {
            var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (underlyingType == typeof(string))
            {
                mapping.SqlType = SqlDbType.NVarChar;
                mapping.Size = 255; // Default size
            }
            else if (underlyingType == typeof(int))
            {
                mapping.SqlType = SqlDbType.Int;
            }
            else if (underlyingType == typeof(long))
            {
                mapping.SqlType = SqlDbType.BigInt;
            }
            else if (underlyingType == typeof(short))
            {
                mapping.SqlType = SqlDbType.SmallInt;
            }
            else if (underlyingType == typeof(byte))
            {
                mapping.SqlType = SqlDbType.TinyInt;
            }
            else if (underlyingType == typeof(bool))
            {
                mapping.SqlType = SqlDbType.Bit;
            }
            else if (underlyingType == typeof(decimal))
            {
                mapping.SqlType = SqlDbType.Decimal;
                mapping.Precision = 18;
                mapping.Scale = 2;
            }
            else if (underlyingType == typeof(double))
            {
                mapping.SqlType = SqlDbType.Float;
            }
            else if (underlyingType == typeof(float))
            {
                mapping.SqlType = SqlDbType.Real;
            }
            else if (underlyingType == typeof(DateTime))
            {
                mapping.SqlType = SqlDbType.DateTime2;
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                mapping.SqlType = SqlDbType.DateTimeOffset;
            }
            else if (underlyingType == typeof(TimeSpan))
            {
                mapping.SqlType = SqlDbType.Time;
            }
            else if (underlyingType == typeof(byte[]))
            {
                mapping.SqlType = SqlDbType.VarBinary;
                mapping.Size = -1; // MAX
            }
            else if (underlyingType == typeof(Guid))
            {
                mapping.SqlType = SqlDbType.UniqueIdentifier;
            }
            else if (underlyingType.IsEnum)
            {
                mapping.SqlType = SqlDbType.Int; // Store enums as integers by default
            }
            else
            {
                // Default to NVARCHAR for complex types (will be serialized)
                mapping.SqlType = SqlDbType.NVarChar;
                mapping.Size = -1; // MAX
            }
        }

        private bool IsNullableType(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private string GenerateColumnDefinition(PropertyMapping mapping)
        {
            var sql = new StringBuilder();
            sql.Append($"{mapping.ColumnName} ");

            // Data type
            sql.Append(this.GetSqlTypeString(mapping));

            // Primary key with auto-increment
            if (mapping.IsPrimaryKey && !this.hasCompositeKey)
            {
                sql.Append(" PRIMARY KEY");
                if (mapping.IsAutoIncrement)
                {
                    sql.Append(" AUTOINCREMENT");
                }
            }

            // Nullability
            if (!mapping.IsNullable && !mapping.IsPrimaryKey)
            {
                sql.Append(" NOT NULL");
            }

            // Unique constraint
            if (mapping.IsUnique && !mapping.IsPrimaryKey)
            {
                sql.Append(" UNIQUE");
            }

            // Default value
            if (mapping.DefaultValue != null)
            {
                sql.Append($" DEFAULT {this.FormatDefaultValue(mapping.DefaultValue, mapping.SqlType)}");
            }

            // Check constraint
            if (!string.IsNullOrEmpty(mapping.CheckConstraint))
            {
                sql.Append($" CHECK ({mapping.CheckConstraint})");
            }

            // Computed column
            if (mapping.IsComputed && !string.IsNullOrEmpty(mapping.ComputedExpression))
            {
                sql.Append($" AS ({mapping.ComputedExpression})");
                if (mapping.IsPersisted)
                {
                    sql.Append(" PERSISTED");
                }
            }

            return sql.ToString();
        }

        private string GetSqlTypeString(PropertyMapping mapping)
        {
            // If SQLiteType is specified, use it directly
            if (mapping.SQLiteType.HasValue)
            {
                switch (mapping.SQLiteType.Value)
                {
                    case SQLiteDbType.Integer:
                        return "INTEGER";
                    case SQLiteDbType.Real:
                        return "REAL";
                    case SQLiteDbType.Text:
                        return "TEXT";
                    case SQLiteDbType.Blob:
                        return "BLOB";
                    case SQLiteDbType.Numeric:
                        return "NUMERIC";
                    default:
                        return "TEXT";
                }
            }

            // Otherwise, convert SqlDbType to SQLite types
            var typeStr = mapping.SqlType.ToString().ToUpper();

            // Handle special cases for SQLite
            switch (mapping.SqlType)
            {
                case SqlDbType.NVarChar:
                case SqlDbType.VarChar:
                case SqlDbType.NChar:
                case SqlDbType.Char:
                    typeStr = "TEXT";
                    break;
                case SqlDbType.Int:
                case SqlDbType.BigInt:
                case SqlDbType.SmallInt:
                case SqlDbType.TinyInt:
                case SqlDbType.Bit:
                    typeStr = "INTEGER";
                    break;
                case SqlDbType.Float:
                case SqlDbType.Real:
                case SqlDbType.Decimal:
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    typeStr = "REAL";
                    break;
                case SqlDbType.Binary:
                case SqlDbType.VarBinary:
                case SqlDbType.Image:
                    typeStr = "BLOB";
                    break;
                case SqlDbType.DateTime:
                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                case SqlDbType.Date:
                case SqlDbType.Time:
                    typeStr = "TEXT"; // SQLite stores dates as text
                    break;
                case SqlDbType.UniqueIdentifier:
                    typeStr = "TEXT";
                    break;
            }

            return typeStr;
        }

        private string FormatDefaultValue(object value, SqlDbType sqlType)
        {
            if (value == null)
                return "NULL";

            if (value is string strValue)
            {
                return $"'{strValue.Replace("'", "''")}'";
            }

            if (value is bool boolValue)
            {
                return boolValue ? "1" : "0";
            }

            if (value is DateTime || value is DateTimeOffset)
            {
                return "datetime('now')";
            }

            if (value.GetType().IsEnum)
            {
                return ((int)value).ToString();
            }

            return value.ToString();
        }

        private string GenerateForeignKeyConstraint(ForeignKeyDefinition fk)
        {
            var sql = new StringBuilder();
            sql.Append($"CONSTRAINT {fk.ConstraintName} ");

            // Handle composite foreign keys
            if (fk.IsComposite)
            {
                var columns = string.Join(", ", fk.ColumnNames);
                var referencedColumns = string.Join(", ", fk.ReferencedColumns);
                sql.Append($"FOREIGN KEY ({columns}) ");
                sql.Append($"REFERENCES {fk.ReferencedTable}({referencedColumns})");
            }
            else
            {
                sql.Append($"FOREIGN KEY ({fk.ColumnName}) ");
                sql.Append($"REFERENCES {fk.ReferencedTable}({fk.ReferencedColumn})");
            }

            if (!string.IsNullOrEmpty(fk.OnDelete))
            {
                sql.Append($" ON DELETE {fk.OnDelete}");
            }

            if (!string.IsNullOrEmpty(fk.OnUpdate))
            {
                sql.Append($" ON UPDATE {fk.OnUpdate}");
            }

            return sql.ToString();
        }

        #endregion
    }
}