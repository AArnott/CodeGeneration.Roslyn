// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeGeneration.Roslyn.Tests.Generators;
using Xunit;

// NOTE THIS FILE INTENTIONALLY SHARES A FILENAME WITH ANOTHER FILE IN THIS PROJECT.
// That's part of the test: can we generate code for two files with the same name?
public partial class CodeGenerationTests
{
    [Fact]
    public void GenerationFromSecondFile()
    {
        var bar = new BarC();
    }

    [DuplicateWithSuffixByType("C")]
    public class Bar
    {
    }
}
