using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGeneration.Roslyn
{
    public class TransformationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransformationContext" /> class.
        /// </summary>
        /// <param name="processingMember">The syntax node the generator attribute is found on.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="compilation">The overall compilation being generated for.</param>
        public TransformationContext(MemberDeclarationSyntax processingMember, SemanticModel semanticModel, CSharpCompilation compilation)
        {
            ProcessingMember = processingMember;
            SemanticModel = semanticModel;
            Compilation = compilation;
        }

        /// <summary>Gets the syntax node the generator attribute is found on.</summary>
        public MemberDeclarationSyntax ProcessingMember { get; }

        /// <summary>Gets the semantic model for the <see cref="Compilation" />.</summary>
        public SemanticModel SemanticModel { get; }

        /// <summary>Gets the overall compilation being generated for.</summary>
        public CSharpCompilation Compilation { get; }
    }
}
