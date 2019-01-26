// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Diagnostics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(AddGeneratedExternGenerator))]
    [Conditional("CodeGeneration")]
    public class AddGeneratedExternAttribute : Attribute
    {
        public AddGeneratedExternAttribute(string @extern)
        {
            Extern = @extern;
        }

        public string Extern { get; }
    }

    public class AddGeneratedExternGenerator : RichBaseGenerator
    {
        public AddGeneratedExternGenerator(AttributeData attributeData)
            : base(attributeData)
        {
            Extern = (string)AttributeData.ConstructorArguments[0].Value;
        }

        public string Extern { get; }

        protected override void Generate(RichGenerationContext context)
        {
            var externAlias = SyntaxFactory.ExternAliasDirective(Extern);
            context.AddExtern(externAlias);
        }
    }
}