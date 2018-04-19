// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using CodeGeneration.Roslyn.Tests.Generators;
using Xunit;

[assembly: DirectoryPath]

public partial class CodeGenerationTests
{
    /// <summary>
    /// Verifies that code generation works because if it didn't, the test wouldn't compile.
    /// </summary>
    [Fact]
    public void SimpleGenerationWorks()
    {
        var foo = new CodeGenerationTests.Foo();
        var fooA = new CodeGenerationTests.FooA();
        var fooB = new CodeGenerationTests.FooB();
        var multiplied = new MultipliedBar();
        multiplied.ValueSuff1020();
        Assert.EndsWith(@"CodeGeneration.Roslyn\src\CodeGeneration.Roslyn.Tests", DirectoryPathTest.Path, StringComparison.OrdinalIgnoreCase);
    }

    [DuplicateWithSuffixByName("A")]
    [DuplicateWithSuffixByType("B")]
    public class Foo
    {
    }
    
    [MultiplySuffix]
    public partial class MultipliedBar
    {
        [Test(X = 10, Y = 20)]
        public string Value { get; set; }
    }
}
