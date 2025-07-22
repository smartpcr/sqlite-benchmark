// -----------------------------------------------------------------------
// <copyright file="ForeignKeyDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Mappings
{
    /// <summary>
    /// Represents a foreign key definition.
    /// </summary>
    public class ForeignKeyDefinition
    {
        public string ConstraintName { get; set; }
        
        /// <summary>
        /// Gets or sets the column names. For composite keys, this will contain multiple columns.
        /// </summary>
        public string[] ColumnNames { get; set; }
        
        /// <summary>
        /// Gets or sets the single column name (for backward compatibility).
        /// </summary>
        public string ColumnName 
        { 
            get => ColumnNames?.Length > 0 ? ColumnNames[0] : null;
            set => ColumnNames = value != null ? new[] { value } : null;
        }
        
        public string ReferencedTable { get; set; }
        
        /// <summary>
        /// Gets or sets the referenced column names. For composite keys, this will contain multiple columns.
        /// </summary>
        public string[] ReferencedColumns { get; set; }
        
        /// <summary>
        /// Gets or sets the single referenced column (for backward compatibility).
        /// </summary>
        public string ReferencedColumn 
        { 
            get => ReferencedColumns?.Length > 0 ? ReferencedColumns[0] : null;
            set => ReferencedColumns = value != null ? new[] { value } : null;
        }
        
        public string OnDelete { get; set; }
        public string OnUpdate { get; set; }
        
        /// <summary>
        /// Gets whether this is a composite foreign key.
        /// </summary>
        public bool IsComposite => ColumnNames?.Length > 1 || ReferencedColumns?.Length > 1;
    }
}