// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;
    using System.Diagnostics;
    using Validation;

    [AttributeUsage(AttributeTargets.Assembly)]
    [CodeGenerationAttribute(typeof(DirectoryPathGenerator))]
    [Conditional("CodeGeneration")]
    public class DirectoryPathAttribute : Attribute
    {
    }
}
