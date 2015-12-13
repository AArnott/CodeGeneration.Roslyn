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
        /// <param name="line">The line number to associate with the error.</param>
        /// <param name="column">The column number to associate with the error.</param>
        void Error(string message, uint line, uint column);

        /// <summary>
        /// Report a warning during the code generation process.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="line">The line number to associate with the warning.</param>
        /// <param name="column">The column number to associate with the warning.</param>
        void Warning(string message, uint line, uint column);

        /// <summary>
        /// Reports incremental progress in the code generation process.
        /// </summary>
        /// <param name="progress">How many steps have been completed out of <paramref name="total"/>.</param>
        /// <param name="total">The total number of steps expected during the generation process.</param>
        void Report(uint progress, uint total);
    }
}
