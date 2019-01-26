// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

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
        /// This type must implement ICodeGenerator.
        /// </param>
        public CodeGenerationAttributeAttribute(string generatorFullTypeName)
        {
            this.GeneratorFullTypeName = generatorFullTypeName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeGenerationAttributeAttribute"/> class.
        /// </summary>
        /// <param name="generatorType">The code generator that implements ICodeGenerator.</param>
        public CodeGenerationAttributeAttribute(Type generatorType)
        {
            if (generatorType == null)
            {
                throw new ArgumentNullException(nameof(generatorType));
            }

            this.GeneratorFullTypeName = generatorType.AssemblyQualifiedName;
        }

        /// <summary>
        /// Gets the fully-qualified type name (including assembly information)
        /// of the code generator to activate.
        /// </summary>
        public string GeneratorFullTypeName { get; }
    }
}
