// -----------------------------------------------------------------------
// <copyright file="DataContractSerializer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Serialization
{
    /// <summary>
    /// DataContract-based serializer implementation.
    /// </summary>
    public class DataContractSerializer<T> : ISerializer<T> where T : class
    {
        private readonly System.Runtime.Serialization.DataContractSerializer serializer;

        public DataContractSerializer()
        {
            this.serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(T));
        }

        public string SerializerType => "DataContract";

        public byte[] Serialize(T entity)
        {
            using var stream = new System.IO.MemoryStream();
            this.serializer.WriteObject(stream, entity);
            return stream.ToArray();
        }

        public T Deserialize(byte[] data)
        {
            using var stream = new System.IO.MemoryStream(data);
            return (T)this.serializer.ReadObject(stream);
        }
    }
}