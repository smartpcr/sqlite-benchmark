// -----------------------------------------------------------------------
// <copyright file="SerializerResolver.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Serialization
{
    using System.Reflection;

    /// <summary>
    /// Resolves the appropriate serializer for an entity type based on attributes.
    /// </summary>
    public static class SerializerResolver
    {
        /// <summary>
        /// Creates a serializer instance for the specified type.
        /// Checks for [JsonConverter] attribute to determine custom serialization.
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <returns>Appropriate serializer instance</returns>
        public static ISerializer<T> CreateSerializer<T>() where T : class
        {
            var type = typeof(T);

            // Check for JsonConverter attribute
            var jsonConverterAttr = type.GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute>();
            if (jsonConverterAttr != null)
            {
                // Use custom converter if specified
                return new JsonSerializer<T>(jsonConverterAttr.ConverterType);
            }

            // Check for DataContract attribute
            var dataContractAttr = type.GetCustomAttribute<System.Runtime.Serialization.DataContractAttribute>();
            if (dataContractAttr != null)
            {
                return new DataContractSerializer<T>();
            }

            // Default to JSON serialization
            return new JsonSerializer<T>();
        }
    }
}