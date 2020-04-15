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

    public class AddExampleBuildPropertyGenerator : ICodeGenerator
    {
        public AddExampleBuildPropertyGenerator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var partialClass = GeneratePartialClass();
            return Task.FromResult(SyntaxFactory.List(partialClass));

            IEnumerable<MemberDeclarationSyntax> GeneratePartialClass()
            {
                var classDeclaration = context.ProcessingNode as ClassDeclarationSyntax;
                yield return classDeclaration
                    .AddMembers(CreateExampleBuildProperty());
            }

            MemberDeclarationSyntax CreateExampleBuildProperty()
            {
                var value = context.BuildProperties["ExampleBuildProperty"];
                return SyntaxFactory.ParseMemberDeclaration($"public string ExampleBuildProperty {{ get; }} = \"{value}\";");
            }
        }
    }
}