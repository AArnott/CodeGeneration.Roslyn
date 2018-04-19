// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Validation;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    public class DirectoryPathGenerator : ICodeGenerator
    {
        public DirectoryPathGenerator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var results = SyntaxFactory.List<MemberDeclarationSyntax>();

            var compilationUnitSyntax = (CompilationUnitSyntax) context.ProcessingMember;
            MemberDeclarationSyntax copy = ClassDeclaration("DirectoryGenerationTest")
                .AddMembers(
                            FieldDeclaration(
                                             VariableDeclaration(
                                                                 PredefinedType(
                                                                                Token(SyntaxKind.StringKeyword)))
                                                 .AddVariables(
                                                               VariableDeclarator(
                                                                                  Identifier("S"))
                                                                   .WithInitializer(
                                                                                    EqualsValueClause(
                                                                                                      LiteralExpression(
                                                                                                                        SyntaxKind.StringLiteralExpression,
                                                                                                                        Literal(context.ProjectDirectory))))))
                                .WithModifiers(
                                               TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.ConstKeyword))));

            results = results.Add(copy);

            return Task.FromResult(results);
        }
    }
}
