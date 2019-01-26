// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using Xunit;

public class DocumentTransformTests : CompilationTestsBase
{
    [Fact]
    public void EmptyFile_NoGenerators()
    {
        AssertGeneratedAsExpected("", "");
    }

    [Fact]
    public void Usings_WhenNoCode_CopiedToOutput()
    {
        const string usings = "using System;";
        AssertGeneratedAsExpected(usings, usings);
    }

    [Fact]
    public void AncestorTree_IsBuiltProperly()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

[EmptyPartial]
partial class Empty {}

namespace Testing.Middle
{
    using System.Linq;

    namespace Inner
    {
        partial class OuterClass<T>
        {
            partial struct InnerStruct<T1, T2>
            {
                [EmptyPartial]
                int Placeholder { get; }
            }
        }
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial class Empty
{
}

namespace Testing.Middle
{
    using System.Linq;

    namespace Inner
    {
        partial class OuterClass<T>
        {
            partial struct InnerStruct<T1, T2>
            {
            }
        }
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void DefineDirective_Dropped()
    {
        // define directives must be leading any other tokens to be valid in C#
        const string source = @"
#define SOMETHING
using System;
using System.Linq;";
        const string generated = @"
using System;
using System.Linq;";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void Comment_BetweenUsings_Dropped()
    {
        const string source = @"
using System;
// one line comment
using System.Linq;";
        const string generated = @"
using System;
using System.Linq;";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void Region_TrailingUsings_Dropped()
    {
        const string source = @"
using System;
#region CustomRegion
using System.Linq;
#endregion //CustomRegion";
        const string generated = @"
using System;
using System.Linq;";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void IfElseDirective_OnUsings_InactiveUsingAndDirectives_Dropped()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;
#if SOMETHING_ACTIVE
using System.Linq;
#elif SOMETHING_INACTIVE
using System.Diagnostics;
#else
using System.Never;
#endif

[EmptyPartial]
partial class Empty {}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;
using System.Linq;

partial class Empty
{
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RegionDirective_InsideClass_Dropped()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial class Empty
{
#region SomeRegion
    [EmptyPartial]
    int Counter { get; }
#endregion
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial class Empty
{
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RegionDirective_InsideStruct_Dropped()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial struct Empty
{
#region SomeRegion
    [EmptyPartial]
    int Counter { get; }
#endregion
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial struct Empty
{
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RegionDirective_InsideNamespace_Dropped()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
#region SomeRegion
    [EmptyPartial]
    partial class Empty { }
#endregion
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial class Empty
    {
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void Class_Modifiers_ArePreserved_WithoutTrivia()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    // some one-line comment
    public static partial class Empty
    {
        [EmptyPartial]
        public static int Method() => 0;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    public static partial class Empty
    {
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void Struct_Modifiers_ArePreserved_WithoutTrivia()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    // some one-line comment
    internal partial struct Empty
    {
        [EmptyPartial]
        public static int Method() => 0;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    internal partial struct Empty
    {
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void Class_TypeParameters_ArePreserved()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial class Empty<T> where T : class
    {
        [EmptyPartial]
        public static T Method() => null;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial class Empty<T>
    {
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void Struct_TypeParameters_ArePreserved()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial struct Empty<T> where T : class
    {
        [EmptyPartial]
        public static T Method() => null;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial struct Empty<T>
    {
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RichGenerator_Wraps_InOtherNamespace()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [DuplicateInOtherNamespace(""Other.Namespace"")]
    class Something
    {
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Other.Namespace
{
    class Something
    {
    }
}";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RichGenerator_Adds_Using()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [AddGeneratedUsing(""System.Collections.Generic"")]
    partial class Something
    {
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;
using System.Collections.Generic;

";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RichGenerator_Adds_ExternAlias()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [AddGeneratedExtern(""MyExternAlias"")]
    partial class Something
    {
    }
}";
        const string generated = @"
extern alias MyExternAlias;

using System;
using CodeGeneration.Roslyn.Tests.Generators;

";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RichGenerator_Adds_Attribute()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [AddGeneratedAttribute(""GeneratedAttribute"")]
    partial class Something
    {
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

[GeneratedAttribute]
";
        AssertGeneratedAsExpected(source, generated);
    }

    [Fact]
    public void RichGenerator_Appends_MultipleResults()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [DuplicateInOtherNamespace(""Other.Namespace1"")]
    [DuplicateInOtherNamespace(""Other.Namespace2"")]
    [AddGeneratedUsing(""System.Collections"")]
    [AddGeneratedUsing(""System.Collections.Generic"")]
    [AddGeneratedExtern(""MyExternAlias1"")]
    [AddGeneratedExtern(""MyExternAlias2"")]
    [AddGeneratedAttribute(""GeneratedAttribute"")]
    [AddGeneratedAttribute(""GeneratedAttribute"")]
    partial class Something
    {
    }
}";
        const string generated = @"
extern alias MyExternAlias1;
extern alias MyExternAlias2;

using System;
using CodeGeneration.Roslyn.Tests.Generators;
using System.Collections;
using System.Collections.Generic;

[GeneratedAttribute]
[GeneratedAttribute]
namespace Other.Namespace1
{
    class Something
    {
    }
}

namespace Other.Namespace2
{
    class Something
    {
    }
}
";
        AssertGeneratedAsExpected(source, generated);
    }
}
