// -----------------------------------------------------------------------
// <copyright file="JsonSerializer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Serialization
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON-based serializer implementation.
    /// </summary>
    public class JsonSerializer<T> : ISerializer<T> where T : class
    {
        private readonly System.Text.Json.JsonSerializerOptions options;
        private readonly Type converterType;

        public JsonSerializer(Type converterType = null)
        {
            this.converterType = converterType;
            this.options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            if (converterType != null)
            {
                if (Activator.CreateInstance(converterType) is JsonConverter converter)
                {
                    this.options.Converters.Add(converter);
                }
            }
        }

        public string SerializerType => this.converterType != null
            ? $"JSON:{this.converterType.Name}"
            : "JSON";

        public byte[] Serialize(T entity)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entity, this.options);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, this.options);
        }
    }
}