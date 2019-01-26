// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using CodeGeneration.Roslyn.Tests.Generators;
using Xunit;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1201 // Namespace should not follow a class

public class NestedNamespacesAndTypesTests
{
    [Fact]
    public void NestedNamespaceTest()
    {
        var nested = new A.B.OuterType.MiddleType.NestedNSTypeA();
    }
}

namespace A
{
    namespace B
    {
        public partial class OuterType
        {
            public partial class MiddleType
            {
                [DuplicateWithSuffixByType("A")]
                public class NestedNSType
                {
                }
            }
        }
    }
}
