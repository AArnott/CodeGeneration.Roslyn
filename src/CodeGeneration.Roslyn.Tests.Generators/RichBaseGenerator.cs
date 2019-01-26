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

    public abstract class RichBaseGenerator : IRichCodeGenerator
    {
        public AttributeData AttributeData { get; }

        protected RichBaseGenerator(AttributeData attributeData)
        {
            AttributeData = attributeData;
        }

        Task<SyntaxList<MemberDeclarationSyntax>> ICodeGenerator.GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<RichGenerationResult> IRichCodeGenerator.GenerateRichAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var richGenerationContext = new RichGenerationContext(context, progress, cancellationToken);
            Generate(richGenerationContext);
            var result = richGenerationContext.CreateResult();
            return Task.FromResult(result);
        }

        protected abstract void Generate(RichGenerationContext context);

        protected class RichGenerationContext
        {
            public RichGenerationContext(TransformationContext transformationContext, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
            {
                TransformationContext = transformationContext;
                Progress = progress;
                CancellationToken = cancellationToken;
            }

            public TransformationContext TransformationContext { get; }

            public IProgress<Diagnostic> Progress { get; }

            public CancellationToken CancellationToken { get; }

            public List<UsingDirectiveSyntax> Usings { get; } = new List<UsingDirectiveSyntax>();

            public List<ExternAliasDirectiveSyntax> Externs { get; } = new List<ExternAliasDirectiveSyntax>();

            public List<AttributeListSyntax> AttributeLists { get; } = new List<AttributeListSyntax>();

            public List<MemberDeclarationSyntax> Members { get; } = new List<MemberDeclarationSyntax>();

            public RichGenerationContext AddUsing(UsingDirectiveSyntax usingDirective)
            {
                Usings.Add(usingDirective);
                return this;
            }

            public RichGenerationContext AddExtern(ExternAliasDirectiveSyntax externAliasDirective)
            {
                Externs.Add(externAliasDirective);
                return this;
            }

            public RichGenerationContext AddAttribute(AttributeSyntax attribute)
            {
                var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
                return AddAttributeList(list);
            }

            public RichGenerationContext AddAttributeList(AttributeListSyntax attributeList)
            {
                AttributeLists.Add(attributeList);
                return this;
            }

            public RichGenerationContext AddMember(MemberDeclarationSyntax memberDeclaration)
            {
                Members.Add(memberDeclaration);
                return this;
            }

            public RichGenerationResult CreateResult()
            {
                return new RichGenerationResult
                {
                    Members = SyntaxFactory.List(Members),
                    Usings = SyntaxFactory.List(Usings),
                    AttributeLists = SyntaxFactory.List(AttributeLists),
                    Externs = SyntaxFactory.List(Externs),
                };
            }
        }
    }
}