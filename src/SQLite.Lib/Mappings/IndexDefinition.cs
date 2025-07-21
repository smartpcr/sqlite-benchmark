// -----------------------------------------------------------------------
// <copyright file="IndexDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an index definition.
    /// </summary>
    public class IndexDefinition
    {
        public string Name { get; set; }
        public List<IndexColumn> Columns { get; set; }
        public bool IsUnique { get; set; }
        public bool IsClustered { get; set; }
        public string Filter { get; set; }
    }
}