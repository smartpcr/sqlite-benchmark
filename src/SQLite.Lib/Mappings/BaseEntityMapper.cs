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
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Base mapper that uses reflection and attributes to create mappings between C# properties and database columns.
    /// </summary>
    /// <typeparam name="T">The entity type to map</typeparam>
    public class BaseEntityMapper<T> where T : class, new()
    {
        private readonly Type entityType;
        private readonly string tableName;
        private readonly string schemaName;
        private readonly Dictionary<PropertyInfo, PropertyMapping> propertyMappings;
        private readonly List<IndexDefinition> indexes;
        private readonly List<ForeignKeyDefinition> foreignKeys;
        private readonly PropertyInfo primaryKeyProperty;
        private readonly bool hasCompositeKey;
        private readonly List<PropertyInfo> compositeKeyProperties;

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

            // Build property mappings
            this.BuildPropertyMappings();

            // Validate primary key
            if (this.primaryKeyProperty == null && !this.hasCompositeKey)
            {
                throw new InvalidOperationException(
                    $"Entity type {this.entityType.Name} must have at least one property marked with [PrimaryKey] or properties named 'Id' or 'Key'");
            }
        }

        /// <summary>
        /// Gets the fully qualified table name including schema.
        /// </summary>
        public virtual string GetFullTableName() => $"{this.schemaName}.{this.tableName}";

        /// <summary>
        /// Gets the table name without schema.
        /// </summary>
        public virtual string GetTableName() => this.tableName;

        /// <summary>
        /// Gets all property mappings.
        /// </summary>
        public IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetPropertyMappings() => this.propertyMappings;

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

        private void BuildPropertyMappings()
        {
            var properties = this.entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var indexGroups = new Dictionary<string, List<IndexColumn>>();

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
                    this.foreignKeys.Add(new ForeignKeyDefinition
                    {
                        ConstraintName = fkAttr.Name ?? $"FK_{this.tableName}_{property.Name}",
                        ColumnName = mapping.ColumnName,
                        ReferencedTable = fkAttr.ReferencedTable,
                        ReferencedColumn = fkAttr.ReferencedColumn ?? "Id",
                        OnDelete = fkAttr.OnDelete,
                        OnUpdate = fkAttr.OnUpdate
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
            if (columnAttr?.SqlType != null)
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

            // Nullability
            mapping.IsNullable = columnAttr?.IsNullable ?? this.IsNullableType(property.PropertyType);

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
            sql.Append($"FOREIGN KEY ({fk.ColumnName}) ");
            sql.Append($"REFERENCES {fk.ReferencedTable}({fk.ReferencedColumn})");

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
    }
}
