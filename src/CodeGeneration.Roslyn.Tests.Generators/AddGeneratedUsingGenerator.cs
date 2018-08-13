// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(AddGeneratedUsingGenerator))]
    [Conditional("CodeGeneration")]
    public class AddGeneratedUsingAttribute : Attribute
    {
        public AddGeneratedUsingAttribute(string @using)
        {
            Using = @using;
        }

        public string Using { get; }
    }

    public class AddGeneratedUsingGenerator : RichBaseGenerator
    {

        public AddGeneratedUsingGenerator(AttributeData attributeData) : base(attributeData)
        {
            Using = (string)AttributeData.ConstructorArguments[0].Value;
        }

        public string Using { get; }

        protected override void Generate(RichGenerationContext context)
        {
            var usingSyntax = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(Using));
            context.AddUsing(usingSyntax);
        }
    }
}