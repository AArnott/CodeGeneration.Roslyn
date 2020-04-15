// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
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
        Assert.EndsWith(Path.Combine("src", "CodeGeneration.Roslyn.Tests"), DirectoryPathTest.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalDependencyFound()
    {
        dynamic d = new Wrapper();
        d.TestMethodSuffix();
    }

    [Fact]
    public void NuGetRecordGeneratorWorks()
    {
        var record = new MyRecord(1, "id");
        record.ToBuilder();
    }

    [Fact]
    public void AccessingBuildPropertiesWorks()
    {
        var objWithBuildProp = new ClassWithExampleBuildProperty();
        Assert.Equal("c7189d5e-495c-4cab-8e18-ab8d7ab71a2e", objWithBuildProp.ExampleBuildProperty);
    }

    public partial class Wrapper
    {
        [ExternalDuplicateWithSuffixByName("Suffix")]
        public void TestMethod()
        {
        }
    }

    [Record]
    public partial class MyRecord
    {
        public int Id { get; }

        public string Name { get; }
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

    [AddExampleBuildProperty]
    public partial class ClassWithExampleBuildProperty
    {
    }
}
