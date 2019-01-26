// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Describes a code generator that responds to attributes on members to generate code,
    /// and returns compilation unit members.
    /// </summary>
    public interface IRichCodeGenerator : ICodeGenerator
    {
        /// <summary>
        /// Create additions to compilation unit representing the expansion of some node to which this attribute is applied.
        /// </summary>
        /// <param name="context">All the inputs necessary to perform the code generation.</param>
        /// <param name="progress">A way to report diagnostic messages.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated syntax nodes to be added to the compilation unit added to the project.</returns>
        Task<RichGenerationResult> GenerateRichAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken);
    }
}
