// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Validation;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    [CodeGeneration("CodeGeneration.Roslyn.Tests.Generators.DuplicateWithSuffixGenerator, CodeGeneration.Roslyn.Tests.Generators, Version=" + ThisAssembly.AssemblyVersion + ", Culture=neutral, PublicKeyToken=null")]
    public class DuplicateWithSuffixAttribute : Attribute
    {
        public DuplicateWithSuffixAttribute(string suffix)
        {
            Requires.NotNullOrEmpty(suffix, nameof(suffix));

            this.Suffix = suffix;
        }

        public string Suffix { get; }
    }
}
