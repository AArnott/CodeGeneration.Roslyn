using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGeneration.Roslyn
{
	public interface ITransformationContext
	{
		MemberDeclarationSyntax ProcessingMember { get; }
		SemanticModel SemanticModel { get; }
		CSharpCompilation Compilation { get; }
		IProgress<Diagnostic> Progress { get; }
	}
}