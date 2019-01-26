// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Diagnostics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(AddGeneratedAttributeGenerator))]
    [Conditional("CodeGeneration")]
    public class AddGeneratedAttributeAttribute : Attribute
    {
        public AddGeneratedAttributeAttribute(string attribute)
        {
            Attribute = attribute;
        }

        public string Attribute { get; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public class GeneratedAttribute : Attribute
    {
    }

    public class AddGeneratedAttributeGenerator : RichBaseGenerator
    {
        public AddGeneratedAttributeGenerator(AttributeData attributeData)
            : base(attributeData)
        {
            Attribute = (string)AttributeData.ConstructorArguments[0].Value;
        }

        public string Attribute { get; }

        protected override void Generate(RichGenerationContext context)
        {
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(Attribute));
            context.AddAttribute(attribute);
        }
    }
}