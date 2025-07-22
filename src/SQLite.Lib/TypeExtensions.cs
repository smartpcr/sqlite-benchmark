// -----------------------------------------------------------------------
// <copyright file="TypeExtensions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;
    using System.Reflection;

    internal static class TypeExtensions
    {
        public static bool IsAnonymousType(this Type type)
        {
            return type.IsClass
                   && type.IsSealed
                   && type.Attributes.HasFlag(TypeAttributes.NotPublic)
                   && type.Name.StartsWith("<>")
                   && type.Name.Contains("AnonymousType");
        }
    }
}