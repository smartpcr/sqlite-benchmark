// -----------------------------------------------------------------------
// <copyright file="CallerInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Contracts
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides information about the caller of a persistence operation.
    /// </summary>
    public class CallerInfo
    {
        /// <summary>
        /// Gets or sets the calling method name (automatically populated).
        /// </summary>
        public string CallerMemberName { get; set; }

        /// <summary>
        /// Gets or sets the source file path (automatically populated).
        /// </summary>
        public string CallerFilePath { get; set; }

        /// <summary>
        /// Gets or sets the line number in the source file (automatically populated).
        /// </summary>
        public int CallerLineNumber { get; set; }

        /// <summary>
        /// Creates a new CallerInfo instance with automatic caller information.
        /// </summary>
        public static CallerInfo Create(
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            return new CallerInfo
            {
                CallerMemberName = memberName,
                CallerFilePath = filePath,
                CallerLineNumber = lineNumber
            };
        }
    }
}