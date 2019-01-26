// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public class EmptyPartialGenerator : ICodeGenerator
    {
        public EmptyPartialGenerator(AttributeData attributeData)
        {
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            if (!(context.ProcessingNode is TypeDeclarationSyntax typeDeclaration))
            {
                return Task.FromResult(SyntaxFactory.List<MemberDeclarationSyntax>());
            }

            var partial = typeDeclaration is StructDeclarationSyntax structDeclaration
                ? StructPartial(structDeclaration)
                : ClassPartial((ClassDeclarationSyntax)typeDeclaration);
            var results = SyntaxFactory.SingletonList(partial);
            return Task.FromResult(results);
        }

        private static MemberDeclarationSyntax ClassPartial(ClassDeclarationSyntax declaration)
        {
            return SyntaxFactory.ClassDeclaration(declaration.Identifier)
                .WithTypeParameterList(declaration.TypeParameterList)
                .WithModifiers(SyntaxTokenList.Create(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));
        }

        private static MemberDeclarationSyntax StructPartial(StructDeclarationSyntax declaration)
        {
            return SyntaxFactory.StructDeclaration(declaration.Identifier)
                .WithTypeParameterList(declaration.TypeParameterList)
                .WithModifiers(SyntaxTokenList.Create(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));
        }
    }
}
