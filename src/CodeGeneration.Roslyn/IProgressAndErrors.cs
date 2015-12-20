// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn
{
    /// <summary>
    /// Provides a callback mechanism by which a code generator can report issues and progress.
    /// </summary>
    public interface IProgressAndErrors
    {
        /// <summary>
        /// Report an error during the code generation process.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="startLine">The starting line number to associate with the error.</param>
        /// <param name="startColumn">The starting column number to associate with the error.</param>
        /// <param name="endLine">The ending line number to associate with the error.</param>
        /// <param name="endColumn">The ending column number to associate with the error.</param>
        /// <param name="subcategory">The subcategory.</param>
        /// <param name="errorCode">The code associated with this message.</param>
        /// <param name="helpKeyword">The help keyword.</param>
        void Error(string message, int startLine, int startColumn, int endLine, int endColumn, string subcategory = null, string errorCode = null, string helpKeyword = "");

        /// <summary>
        /// Report a warning during the code generation process.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="startLine">The starting line number to associate with the warning.</param>
        /// <param name="startColumn">The starting column number to associate with the warning.</param>
        /// <param name="endLine">The ending line number to associate with the warning.</param>
        /// <param name="endColumn">The ending column number to associate with the warning.</param>
        /// <param name="subcategory">The subcategory.</param>
        /// <param name="warningCode">The code associated with this message.</param>
        /// <param name="helpKeyword">The help keyword.</param>
        void Warning(string message, int startLine, int startColumn, int endLine, int endColumn, string subcategory = null, string warningCode = null, string helpKeyword = "");
    }
}
