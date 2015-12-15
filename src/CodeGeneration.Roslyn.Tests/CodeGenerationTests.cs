// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeGeneration.Roslyn.Tests.Generators;
using Xunit;

public class CodeGenerationTests
{
    /// <summary>
    /// Verifies that code generation works because if it didn't, the test wouldn't compile.
    /// </summary>
    [Fact]
    public void SimpleGenerationWorks()
    {
        var foo = new Foo();
        var fooA = new FooA();
        var fooB = new FooB();
    }

    [DuplicateWithSuffixByName("A")]
    [DuplicateWithSuffixByType("B")]
    public class Foo
    {
    }
}
