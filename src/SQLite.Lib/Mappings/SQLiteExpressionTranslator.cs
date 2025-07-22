// -----------------------------------------------------------------------
// <copyright file="SQLiteExpressionTranslator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using SQLite.Lib.Contracts;

    /// <summary>
    /// Translates LINQ Expression trees to SQL WHERE clauses.
    /// </summary>
    public class SQLiteExpressionTranslator<T> : ExpressionVisitor
    {
        private readonly StringBuilder sql = new StringBuilder();
        private readonly Dictionary<string, object> parameters = new Dictionary<string, object>();
        private readonly IReadOnlyDictionary<System.Reflection.PropertyInfo, PropertyMapping> propertyMappings;
        private readonly Func<string> getPrimaryKeyColumn;
        private int parameterIndex = 0;

        public SQLiteExpressionTranslator() : this(null, null)
        {
        }

        public SQLiteExpressionTranslator(
            IReadOnlyDictionary<System.Reflection.PropertyInfo, PropertyMapping> propertyMappings,
            Func<string> getPrimaryKeyColumn)
        {
            this.propertyMappings = propertyMappings;
            this.getPrimaryKeyColumn = getPrimaryKeyColumn;
        }

        public class TranslationResult
        {
            public string Sql { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
        }

        public TranslationResult Translate(Expression<Func<T, bool>> expression)
        {
            this.sql.Clear();
            this.Visit(expression.Body);
            return new TranslationResult
            {
                Sql = this.sql.ToString(),
                Parameters = this.parameters
            };
        }

        public string TranslateOrderBy(Expression<Func<T, IComparable>> orderBy, bool ascending = true)
        {
            if (orderBy == null)
                return string.Empty;

            // Extract the property name from the orderBy expression
            var memberExpression = orderBy.Body as MemberExpression;
            if (memberExpression == null && orderBy.Body is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }

            if (memberExpression != null)
            {
                var propertyName = memberExpression.Member.Name;
                var columnName = this.GetColumnNameFromMapper(propertyName);
                return $"{columnName} {(ascending ? "ASC" : "DESC")}";
            }

            return string.Empty;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            this.sql.Append("(");

            this.Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    this.sql.Append(" = ");
                    break;
                case ExpressionType.NotEqual:
                    this.sql.Append(" <> ");
                    break;
                case ExpressionType.LessThan:
                    this.sql.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    this.sql.Append(" <= ");
                    break;
                case ExpressionType.GreaterThan:
                    this.sql.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    this.sql.Append(" >= ");
                    break;
                case ExpressionType.AndAlso:
                    this.sql.Append(" AND ");
                    break;
                case ExpressionType.OrElse:
                    this.sql.Append(" OR ");
                    break;
                default:
                    throw new NotSupportedException($"Binary operator {node.NodeType} is not supported");
            }

            this.Visit(node.Right);

            this.sql.Append(")");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                // This is a property access on the parameter (e.g., x.UpdateName)
                this.sql.Append(this.GetColumnName(node.Member.Name));
            }
            else
            {
                // This is a constant value access
                var value = this.GetValue(node);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = value;
                this.sql.Append(paramName);
            }

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var paramName = $"@p{this.parameterIndex++}";
            this.parameters[paramName] = node.Value;
            this.sql.Append(paramName);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains")
            {
                if (node.Object != null)
                {
                    // String.Contains
                    this.Visit(node.Object);
                    this.sql.Append(" LIKE ");
                    var value = GetValue(node.Arguments[0]);
                    var paramName = $"@p{this.parameterIndex++}";
                    this.parameters[paramName] = $"%{value}%";
                    this.sql.Append(paramName);
                }
                else if (node.Arguments.Count == 2)
                {
                    // List.Contains
                    this.Visit(node.Arguments[1]);
                    this.sql.Append(" IN (");
                    var values = GetValue(node.Arguments[0]) as IEnumerable;
                    var paramNames = new List<string>();
                    foreach (var value in values)
                    {
                        var paramName = $"@p{this.parameterIndex++}";
                        this.parameters[paramName] = value;
                        paramNames.Add(paramName);
                    }
                    this.sql.Append(string.Join(", ", paramNames));
                    this.sql.Append(")");
                }
            }
            else if (node.Method.Name == "StartsWith")
            {
                Visit(node.Object);
                this.sql.Append(" LIKE ");
                var value = GetValue(node.Arguments[0]);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = $"{value}%";
                this.sql.Append(paramName);
            }
            else if (node.Method.Name == "EndsWith")
            {
                Visit(node.Object);
                this.sql.Append(" LIKE ");
                var value = GetValue(node.Arguments[0]);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = $"%{value}";
                this.sql.Append(paramName);
            }
            else
            {
                throw new NotSupportedException($"Method {node.Method.Name} is not supported");
            }

            return node;
        }

        private string GetColumnName(string propertyName)
        {
            return this.GetColumnNameFromMapper(propertyName);
        }

        private string GetColumnNameFromMapper(string propertyName)
        {
            // If property mappings are available, use them to get the correct column name
            if (this.propertyMappings != null)
            {
                var propertyMapping = this.propertyMappings
                    .FirstOrDefault(p => p.Key.Name == propertyName);
                
                if (propertyMapping.Value != null)
                {
                    return propertyMapping.Value.ColumnName;
                }
            }

            // Fallback to property name mappings
            return propertyName switch
            {
                "Id" => this.GetPrimaryKeyColumn(),
                "Key" => this.GetPrimaryKeyColumn(),
                _ => propertyName
            };
        }

        private string GetPrimaryKeyColumn()
        {
            // If getPrimaryKeyColumn delegate is available, use it
            if (this.getPrimaryKeyColumn != null)
            {
                return this.getPrimaryKeyColumn();
            }

            // Fallback to type-based determination
            var type = typeof(T);
            if (type.Name == "UpdateEntity") return "UpdateId";
            if (type.Name == "CacheEntry") return "CacheKey";
            return "Id";
        }

        private object GetValue(Expression expression)
        {
            var lambda = Expression.Lambda(expression);
            var compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }
    }
}