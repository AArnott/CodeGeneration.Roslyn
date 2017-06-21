using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGeneration.Roslyn
{
    internal class TransformationContext : ITransformationContext
    {
        public MemberDeclarationSyntax ProcessingMember { get; }
        public SemanticModel SemanticModel { get; }
        public CSharpCompilation Compilation { get; }
        public IProgress<Diagnostic> Progress { get; }

        public TransformationContext(MemberDeclarationSyntax processingMember, SemanticModel semanticModel, CSharpCompilation compilation, IProgress<Diagnostic> progress)
        {
            ProcessingMember = processingMember;
            SemanticModel = semanticModel;
            Compilation = compilation;
            Progress = progress;
        }
    }
}
