// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public class DuplicateInOtherNamespaceGenerator : RichBaseGenerator
    {
        public DuplicateInOtherNamespaceGenerator(AttributeData attributeData)
            : base(attributeData)
        {
            Namespace = (string)AttributeData.ConstructorArguments[0].Value;
        }

        public string Namespace { get; }

        protected override void Generate(RichGenerationContext context)
        {
            if (!(context.TransformationContext.ProcessingNode is ClassDeclarationSyntax classDeclaration))
            {
                return;
            }
            var partial = SyntaxFactory.ClassDeclaration(classDeclaration.Identifier);
            var namespaceSyntax =
                SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.ParseName(Namespace))
                    .AddMembers(partial);

            context.AddMember(namespaceSyntax);
        }
    }
}