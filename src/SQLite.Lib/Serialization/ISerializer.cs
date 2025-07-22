// -----------------------------------------------------------------------
// <copyright file="ISerializer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Serialization
{
    /// <summary>
    /// Defines the contract for entity serialization.
    /// </summary>
    /// <typeparam name="T">The entity type to serialize</typeparam>
    public interface ISerializer<T> where T : class
    {
        /// <summary>
        /// Serializes an entity to a byte array.
        /// </summary>
        /// <param name="entity">The entity to serialize</param>
        /// <returns>Serialized byte array</returns>
        byte[] Serialize(T entity);

        /// <summary>
        /// Deserializes an entity from a byte array.
        /// </summary>
        /// <param name="data">The byte array to deserialize</param>
        /// <returns>Deserialized entity</returns>
        T Deserialize(byte[] data);

        /// <summary>
        /// Gets the serializer type name for metadata tracking.
        /// </summary>
        string SerializerType { get; }
    }
}