// -----------------------------------------------------------------------
// <copyright file="EntityAlreadyExistsException.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib
{
    using System;

    /// <summary>
    /// Exception thrown when attempting to create an entity that already exists.
    /// </summary>
    public class EntityAlreadyExistsException : InvalidOperationException
    {
        public string EntityKey { get; }

        public EntityAlreadyExistsException(string entityKey, string message) : base(message)
        {
            this.EntityKey = entityKey;
        }
    }
}