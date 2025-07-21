// -----------------------------------------------------------------------
// <copyright file="UnsafeSizeCalculator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Entities
{
    using System;
    using System.Runtime.InteropServices;

    public unsafe class UnsafeSizeCalculator
    {
        [StructLayout(LayoutKind.Sequential)]
        struct ObjectHeader
        {
            public IntPtr MethodTable;
            public IntPtr SyncBlockIndex;
        }

        public static int GetExactSize<T>() where T : unmanaged
        {
            return sizeof(T);
        }

        // Get size using pointer arithmetic (be very careful!)
        public static unsafe long GetObjectSize(object obj)
        {
            if (obj == null) return 0;

            var type = obj.GetType();

            if (type.IsValueType)
            {
                return Marshal.SizeOf(obj);
            }

            // This is highly unsafe and platform-dependent
            // Only for educational purposes!
            var tr = __makeref(obj);
            var ptr = **(IntPtr**)(&tr);

            // Read the method table pointer (first field in object header)
            var methodTable = *(IntPtr*)ptr;

            // The size is often stored at a specific offset in the method table
            // This is highly implementation-specific!
            // return *(int*)(methodTable + 4);

            // Safer to use our estimation method
            return UnsafeSizeCalculator.EstimateObjectSize(obj);
        }

        private static long EstimateObjectSize(object obj)
        {
            return MemorySizeEstimator.EstimateObjectSize(obj);
        }
    }
}