// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public abstract class BaseGenerator : ICodeGenerator
    {
        public AttributeData AttributeData { get; }

        protected BaseGenerator(AttributeData attributeData)
        {
            AttributeData = attributeData;
        }

        Task<SyntaxList<MemberDeclarationSyntax>> ICodeGenerator.GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var richGenerationContext = new GenerationContext(context, progress, cancellationToken);
            Generate(richGenerationContext);
            var result = richGenerationContext.CreateResult();
            return Task.FromResult(result);
        }

        protected abstract void Generate(GenerationContext context);

        protected class GenerationContext
        {
            public GenerationContext(TransformationContext transformationContext, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
            {
                TransformationContext = transformationContext;
                Progress = progress;
                CancellationToken = cancellationToken;
            }

            public TransformationContext TransformationContext { get; }

            public IProgress<Diagnostic> Progress { get; }

            public CancellationToken CancellationToken { get; }

            public List<MemberDeclarationSyntax> Members { get; } = new List<MemberDeclarationSyntax>();

            public GenerationContext AddMember(MemberDeclarationSyntax memberDeclaration)
            {
                Members.Add(memberDeclaration);
                return this;
            }

            public SyntaxList<MemberDeclarationSyntax> CreateResult()
            {
                return SyntaxFactory.List(Members);
            }
        }
    }
}