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
    [Fact]
    public void EmptyTest()
    {
        var foo = new Foo();
        var fooA = new FooA();
    }

    [DuplicateWithSuffix("A")]
    public class Foo { }
}
