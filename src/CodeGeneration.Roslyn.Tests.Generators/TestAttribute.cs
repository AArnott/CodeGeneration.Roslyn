// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tests.Generators
{
    using System;

    public class TestAttribute : Attribute
    {
        public int X { get; set; }

        public int Y { get; set; }
    }
}
