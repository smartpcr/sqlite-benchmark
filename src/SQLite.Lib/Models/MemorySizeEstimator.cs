// -----------------------------------------------------------------------
// <copyright file="MemorySizeEstimator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization.Formatters.Binary;

    public class MemorySizeEstimator
    {
        // Method 1: Using Marshal.SizeOf (only for value types and structs without references)
        public static int GetMarshalSize<T>(T obj) where T : struct
        {
            return Marshal.SizeOf(obj);
        }

        // Method 2: Binary Serialization (rough estimate, includes metadata)
        [Obsolete("BinaryFormatter is obsolete and should not be used for production code")]
        public static long GetSerializedSize(object obj)
        {
            using var stream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, obj);
            return stream.Length;
        }

        // Method 3: Using GC to measure allocated memory
        public static long MeasureMemoryUsage<T>(Func<T> factory)
        {
            // Clean up memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetTotalMemory(true);
            T obj = factory();
            var after = GC.GetTotalMemory(true);

            // Keep object alive
            GC.KeepAlive(obj);

            return after - before;
        }

        // Method 4: Reflection-based size estimation
        public static long EstimateObjectSize(object obj)
        {
            return EstimateObjectSize(obj, new HashSet<object>());
        }

        private static long EstimateObjectSize(object obj, HashSet<object> visited)
        {
            if (obj == null)
                return 0;

            var type = obj.GetType();

            // Avoid infinite recursion
            if (!visited.Add(obj))
                return 0;

            long size = 0;

            // Base overhead for any object (header + method table pointer)
            size += IntPtr.Size == 8 ? 24 : 12; // 64-bit vs 32-bit

            if (type.IsPrimitive)
            {
                size += GetPrimitiveSize(type);
            }
            else if (type == typeof(string))
            {
                size += IntPtr.Size == 8 ? 24 : 14; // String overhead
                size += ((string)obj).Length * sizeof(char);
            }
            else if (type.IsArray)
            {
                var array = (Array)obj;
                size += IntPtr.Size == 8 ? 24 : 12; // Array overhead
                size += array.Length * IntPtr.Size; // Length field

                var elementType = type.GetElementType()!;
                if (elementType.IsPrimitive)
                {
                    size += array.Length * GetPrimitiveSize(elementType);
                }
                else
                {
                    foreach (var item in array)
                    {
                        size += EstimateObjectSize(item, visited);
                    }
                }
            }
            else if (obj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    size += EstimateObjectSize(item, visited);
                }
            }
            else
            {
                // Class or struct
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType.IsPrimitive)
                    {
                        size += GetPrimitiveSize(field.FieldType);
                    }
                    else
                    {
                        var fieldValue = field.GetValue(obj);
                        if (fieldValue != null)
                        {
                            size += IntPtr.Size; // Reference size
                            size += EstimateObjectSize(fieldValue, visited);
                        }
                    }
                }
            }

            return size;
        }

        private static int GetPrimitiveSize(Type type)
        {
            if (type == typeof(bool)) return sizeof(bool);
            if (type == typeof(byte)) return sizeof(byte);
            if (type == typeof(sbyte)) return sizeof(sbyte);
            if (type == typeof(char)) return sizeof(char);
            if (type == typeof(short)) return sizeof(short);
            if (type == typeof(ushort)) return sizeof(ushort);
            if (type == typeof(int)) return sizeof(int);
            if (type == typeof(uint)) return sizeof(uint);
            if (type == typeof(long)) return sizeof(long);
            if (type == typeof(ulong)) return sizeof(ulong);
            if (type == typeof(float)) return sizeof(float);
            if (type == typeof(double)) return sizeof(double);
            if (type == typeof(decimal)) return sizeof(decimal);
            if (type == typeof(IntPtr)) return IntPtr.Size;
            if (type == typeof(UIntPtr)) return UIntPtr.Size;
            return IntPtr.Size; // Default to pointer size
        }
    }
}