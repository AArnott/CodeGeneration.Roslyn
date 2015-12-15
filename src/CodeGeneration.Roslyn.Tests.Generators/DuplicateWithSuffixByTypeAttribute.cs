// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using Validation;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(DuplicateWithSuffixGenerator))]
    public class DuplicateWithSuffixByTypeAttribute : Attribute
    {
        public DuplicateWithSuffixByTypeAttribute(string suffix)
        {
            Requires.NotNullOrEmpty(suffix, nameof(suffix));

            this.Suffix = suffix;
        }

        public string Suffix { get; }
    }
}
