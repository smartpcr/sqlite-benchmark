// -----------------------------------------------------------------------
// <copyright file="CacheEntryMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System.Data;
    using SQLite.Lib.Contracts;
    using SQLite.Lib.Models;
    using SQLite.Lib.Serialization;

    /// <summary>
    /// Specialized mapper for CacheEntry&lt;T&gt; entities that handles generic value serialization.
    /// </summary>
    /// <typeparam name="T">The type of value stored in the cache entry</typeparam>
    public class CacheEntryMapper<T> : BaseEntityMapper<CacheEntry<T>, string> where T : class, IEntity<string>
    {
        private readonly ISerializer<T> valueSerializer;

        public CacheEntryMapper(ISerializer<T> valueSerializer = null)
        {
            this.valueSerializer = valueSerializer ?? SerializerResolver.GetSerializer<T>();
        }

        /// <summary>
        /// Override to use consistent full table name (SQLite doesn't use schemas).
        /// </summary>
        public override string GetFullTableName() => this.GetTableName(); // SQLite doesn't use schemas

        /// <summary>
        /// Override to add parameters with proper value serialization.
        /// </summary>
        public override void AddParameters(System.Data.Common.DbCommand command, CacheEntry<T> entity)
        {
            // Serialize the value before adding parameters
            if (entity.Value != null)
            {
                entity.Data = this.valueSerializer.Serialize(entity.Value);
                entity.Size = entity.Data?.Length ?? 0;
            }

            // Ensure required fields are set
            if (string.IsNullOrEmpty(entity.TypeName))
            {
                entity.TypeName = typeof(T).Name;
            }
            if (string.IsNullOrEmpty(entity.AssemblyVersion))
            {
                entity.AssemblyVersion = this.GetAssemblyVersion();
            }

            // Use base class to add parameters based on property mappings
            base.AddParameters(command, entity);
        }

        /// <summary>
        /// Override to map from reader with proper value deserialization.
        /// </summary>
        public override CacheEntry<T> MapFromReader(IDataReader reader)
        {
            // Use base class to map all properties
            var entity = base.MapFromReader(reader);

            // Deserialize the value from the Data column
            if (entity.Data != null && entity.Data.Length > 0)
            {
                entity.Value = this.valueSerializer.Deserialize(entity.Data);
            }

            return entity;
        }

        /// <summary>
        /// Gets the assembly version for type tracking.
        /// </summary>
        protected virtual string GetAssemblyVersion()
        {
            var assembly = typeof(T).Assembly;
            return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        }
    }
}