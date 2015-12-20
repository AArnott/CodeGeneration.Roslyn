// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

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

    public class DuplicateWithSuffixGenerator : ICodeGenerator
    {
        private readonly AttributeData attributeData;
        private readonly ImmutableDictionary<string, TypedConstant> data;
        private readonly string suffix;

        public DuplicateWithSuffixGenerator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));

            this.suffix = (string)attributeData.ConstructorArguments[0].Value;
            this.attributeData = attributeData;
            this.data = this.attributeData.NamedArguments.ToImmutableDictionary(kv => kv.Key, kv => kv.Value);
        }

        public Task<IReadOnlyList<MemberDeclarationSyntax>> GenerateAsync(MemberDeclarationSyntax applyTo, Document document, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var results = new List<MemberDeclarationSyntax>();

            MemberDeclarationSyntax copy = null;
            var applyToClass = applyTo as ClassDeclarationSyntax;
            if (applyToClass != null)
            {
                copy = applyToClass
                    .WithIdentifier(SyntaxFactory.Identifier(applyToClass.Identifier.ValueText + this.suffix));
            }

            if (copy != null)
            {
                results.Add(copy);
            }

            return Task.FromResult<IReadOnlyList<MemberDeclarationSyntax>>(results);
        }
    }
}
