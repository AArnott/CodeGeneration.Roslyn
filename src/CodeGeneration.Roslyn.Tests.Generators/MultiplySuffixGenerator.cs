// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Validation;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    public class MultiplySuffixGenerator : ICodeGenerator
    {
        public MultiplySuffixGenerator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var results = SyntaxFactory.List<MemberDeclarationSyntax>();

            MemberDeclarationSyntax copy = null;
            var applyToClass = context.ProcessingNode as ClassDeclarationSyntax;
            if (applyToClass != null)
            {
                var properties = applyToClass.Members.OfType<PropertyDeclarationSyntax>()
                    .Select(x =>
                    {
                        var propertySymbol = context.SemanticModel.GetDeclaredSymbol(x);
                        var attribute = propertySymbol?.GetAttributes()
                            .FirstOrDefault(a => a.AttributeClass.Name == nameof(TestAttribute));
                        string suffix = "Suff" + string.Concat(attribute?.NamedArguments.Select(a => a.Value.Value.ToString()) ?? Enumerable.Empty<string>());
                        return (MemberDeclarationSyntax)MethodDeclaration(ParseTypeName("void"), x.Identifier.ValueText + suffix)
                            .AddModifiers(Token(SyntaxKind.PublicKeyword))
                            .AddBodyStatements(Block());
                    });
                copy = ClassDeclaration(applyToClass.Identifier).WithModifiers(applyToClass.Modifiers).AddMembers(properties.ToArray());
            }

            if (copy != null)
            {
                results = results.Add(copy);
            }

            return Task.FromResult(results);
        }
    }
}
