// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn
{
    using System;

    /// <summary>
    /// A base attribute type for code generation attributes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class CodeGenerationAttributeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeGenerationAttributeAttribute"/> class.
        /// </summary>
        /// <param name="generatorFullTypeName">
        /// The fully-qualified type name (including assembly information)
        /// of the code generator to activate.
        /// </param>
        public CodeGenerationAttributeAttribute(string generatorFullTypeName)
        {
            this.GeneratorFullTypeName = generatorFullTypeName;
        }

        /// <summary>
        /// Gets the fully-qualified type name (including assembly information)
        /// of the code generator to activate.
        /// </summary>
        public string GeneratorFullTypeName { get; }
    }
}
