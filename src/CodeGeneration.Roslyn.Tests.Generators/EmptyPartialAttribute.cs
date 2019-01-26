// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Diagnostics;

    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    [CodeGenerationAttribute(typeof(EmptyPartialGenerator))]
    [Conditional("CodeGeneration")]
    public class EmptyPartialAttribute : Attribute
    {
    }
}