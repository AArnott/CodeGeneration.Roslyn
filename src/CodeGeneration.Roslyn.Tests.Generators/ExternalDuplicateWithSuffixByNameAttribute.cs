// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Diagnostics;
    using Validation;

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute("CodeGeneration.Roslyn.Tests.Generators.ExternalDuplicateWithSuffixGenerator, CodeGeneration.Roslyn.Tests.Generators, Version=" + ThisAssembly.AssemblyVersion + ", Culture=neutral, PublicKeyToken=null")]
    [Conditional("CodeGeneration")]
    public class ExternalDuplicateWithSuffixByNameAttribute : Attribute
    {
        public ExternalDuplicateWithSuffixByNameAttribute(string suffix)
        {
            Requires.NotNullOrEmpty(suffix, nameof(suffix));

            this.Suffix = suffix;
        }

        public string Suffix { get; }
    }
}
