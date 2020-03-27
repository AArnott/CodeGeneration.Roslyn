# Roslyn-based Code Generation

[![Build Status](https://andrewarnott.visualstudio.com/OSS/_apis/build/status/CodeGeneration.Roslyn)](https://andrewarnott.visualstudio.com/OSS/_build/latest?definitionId=15)
[![GitHub Actions CI status](https://github.com/AArnott/CodeGeneration.Roslyn/workflows/CI/badge.svg?branch=master)](https://github.com/AArnott/CodeGeneration.Roslyn/actions?query=workflow%3ACI+branch%3Amaster)
[![NuGet package](https://img.shields.io/nuget/v/CodeGeneration.Roslyn.svg)][NuPkg]

Assists in performing Roslyn-based code generation during a build.
This includes design-time support, such that code generation can respond to
changes made in hand-authored code files by generating new code that shows
up to Intellisense as soon as the file is saved to disk.

See [who's generating code or consuming it using CodeGeneration.Roslyn](https://github.com/AArnott/CodeGeneration.Roslyn/wiki/Users).

Instructions on development and using this project's source code are in [CONTRIBUTING.md](CONTRIBUTING.md).

## Table of Contents

- [Roslyn-based Code Generation](#roslyn-based-code-generation)
  - [Table of Contents](#table-of-contents)
  - [How to write your own code generator](#how-to-write-your-own-code-generator)
    - [Prerequisites](#prerequisites)
    - [Define code generator](#define-code-generator)
    - [Define attribute](#define-attribute)
    - [Create consuming console app](#create-consuming-console-app)
    - [Apply code generation](#apply-code-generation)
  - [Advanced scenarios](#advanced-scenarios)
    - [Separate out the attribute](#separate-out-the-attribute)
    - [Customize generator reference](#customize-generator-reference)
      - [Multitargeting generator](#multitargeting-generator)
    - [Package your code generator](#package-your-code-generator)

## How to write your own code generator

In this walkthrough, we will define a code generator that replicates any class (annotated with our custom attribute) with a suffix (specified in the attribute) appended to its name.

### Prerequisites

* [.NET Core SDK v2.1+][dotnet-sdk-2.1]
  
  If you don't have v2.1+ there will be cryptic error messages
  (see [#111](https://github.com/AArnott/CodeGeneration.Roslyn/issues/111)).

[dotnet-sdk-2.1]: https://dotnet.microsoft.com/download/dotnet-core/2.1

### Define code generator

Your generator cannot be defined in the same project that will have code generated
for it. That's because code generation runs *before* the consuming project is itself compiled. We'll start by creating the generator. This must be done in a library that targets `netcoreapp2.1`. Let's create one called Duplicator:

> `dotnet new classlib -f netcoreapp2.1 -o Duplicator`

Now we'll use an [MSBuild project SDK] `CodeGeneration.Roslyn.Plugin.Sdk` to speed up configuring our generator plugin. Edit your project file and add the `<Sdk>` element:

```xml
<!-- Duplicator/Duplicator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Add the following element above any others: -->
  <Sdk Name="CodeGeneration.Roslyn.Plugin.Sdk" Version="{replace with actual version used}"/>

  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>
```

This SDK will automatically add a PackageReference to the corresponding version of [`CodeGeneration.Roslyn`][NuPkg] nuget, and set properties to make plugin build correctly. We can now write our generator:

> ⚠ *Note: constructor is required to have exactly a single `AttributeData` parameter.*

```csharp
// Duplicator/DuplicateWithSuffixGenerator.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duplicator
{
    public class DuplicateWithSuffixGenerator : ICodeGenerator
    {
        private readonly string suffix;

        public DuplicateWithSuffixGenerator(AttributeData attributeData)
        {
            suffix = (string)attributeData.ConstructorArguments[0].Value;
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            // Our generator is applied to any class that our attribute is applied to.
            var applyToClass = (ClassDeclarationSyntax)context.ProcessingNode;

            // Apply a suffix to the name of a copy of the class.
            var copy = applyToClass.WithIdentifier(SyntaxFactory.Identifier(applyToClass.Identifier.ValueText + suffix));

            // Return our modified copy. It will be added to the user's project for compilation.
            var results = SyntaxFactory.SingletonList<MemberDeclarationSyntax>(copy);
            return Task.FromResult(results);
        }
    }
}
```

### Define attribute

To activate your code generator, you need to define an attribute with which
we'll annotate the class to be copied. Define your attribute class:

```csharp
// Duplicator/DuplicateWithSuffixAttribute.cs
using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;

namespace Duplicator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(DuplicateWithSuffixGenerator))]
    [Conditional("CodeGeneration")]
    public class DuplicateWithSuffixAttribute : Attribute
    {
        public DuplicateWithSuffixAttribute(string suffix)
        {
            Suffix = suffix;
        }

        public string Suffix { get; }
    }
}
```

The `[Conditional("CodeGeneration")]` attribute is not necessary, but it will prevent
the attribute from persisting in the compiled assembly that consumes it, leaving it
instead as just a compile-time hint to code generation, and allowing you to not ship
with a dependency on your code generation assembly.

### Create consuming console app

We'll consume our generator in a Reflector app:
> `dotnet new console -f netcoreapp2.1 -o Reflector`
> 
> `dotnet add Reflector reference Duplicator`

Let's write a simple program that prints all types in its assembly:
```csharp
// Reflector/Program.cs
using System;

namespace Reflector
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var type in typeof(Program).Assembly.GetTypes())
                Console.WriteLine(type.FullName);
        }
    }
}
```

Now, when we `dotnet run -p Reflector` we should get:
> `Reflector.Program`

### Apply code generation

Applying code generation is incredibly simple. Just add the attribute on any type
or member supported by the attribute and generator you wrote. We'll create a new
class to test our Duplicator on:

```csharp
// Reflector/Test.cs
using Duplicator;

namespace Reflector
{
    [DuplicateWithSuffix("Passed")]
    class Test {}
}
```

Right now `dotnet run -p Reflector` outputs:
> `Reflector.Program`
> 
> `Reflector.Test`

Now all that's left is to plumb the build pipeline with code generation tool.
You'll need to add a reference to [`CodeGeneration.Roslyn.Tool`][ToolNuPkg] package:
> `dotnet add Reflector package CodeGeneration.Roslyn.Tool`

Also, you need to add the following metadata to your generator project reference:
`OutputItemType="CodeGenerationRoslynPlugin"`. This will add the path to the `Duplicator.dll` to the list of plugins the tool runs.

This is how your project file can look like:

```xml
<!-- Reflector/Reflector.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- This ProjectReference to the generator need to have the OutputItemType metadata -->
    <ProjectReference Include="..\Duplicator\Duplicator.csproj"
                      OutputItemType="CodeGenerationRoslynPlugin" />
    <!--
      This contains the generation tool and MSBuild targets that invoke it,
      and so can be marked with PrivateAssets="all"
    -->
    <PackageReference Include="CodeGeneration.Roslyn.Tool"
                      Version="{replace with actual version used}"
                      PrivateAssets="all" />
  </ItemGroup>
</Project>
```

And if all steps were done correctly, `dotnet run -p Reflector` should print:
> `Reflector.Program`
> 
> `Reflector.Test`
> 
> `Reflector.TestPassed`

> 💡 Notice that there is a `TestPassed` type in the assembly now.

What's even better is that you should see that new type in IntelliSense as well!
Try executing Go to Definition (<kbd>F12</kbd>) on it - your IDE (VS/VS Code) should open the generated file for you (it'll be located in `IntermediateOutputPath` - most commonly `obj/`).

## Advanced scenarios

While the sample worked, it was also unrealistically simple and skipped many
complex issues that will inevitably arise when you try to use this project
in real world. What follows is a deep dive into more realistic solutions.

Most of the issues are about two things: TargetFramework and dependecies.
TargetFramework/TFM/TxM (e.g. `netcoreapp2.1`) of the generator is restricted
to an exact version and there's not a lot of wiggle room there. In contrast,
projects consuming generators and their outputs will target any existing TFM.
If you will try to add a P2P (project-to-project) reference to a generator
(targeting `netcoreapp2.1`) to a business model project targeting `netstandard1.0`,
you'll get errors.

### Separate out the attribute

The triggering attribute has to be available in consuming code, but the generator
doesn't. So we'll separate the attribute into another project that has TFM
allowing all your consuming projects to reference it, for example `netstandard1.0`.
This new project (building on the demo sample, let's call it `Duplicator.Attributes`)
needs only to know about `CodeGenerationAttributeAttribute` type which is available
in [`CodeGeneration.Roslyn.Attributes`][AttrNuPkg] package. That package has a wide
selection of TFMs available so it shouldn't be a problematic reference. Additionally,
if you annotate your triggering attribute with `[Conditional("...")]`, you can make
any references to the attribute project/package non-runtime via `PrivateAssets="all"`.

But, if the attribute project doesn't have a reference to generator project,
we cannot use `typeof(DuplicateWithSuffixGenerator)`. To workaround this,
we must write the necessary string manually: an assembly-qualified generator type name.
> `[CodeGenerationAttribute("Duplicator.DuplicateWithSuffixGenerator, Duplicator")]`

So, the consuming project will have a simple ProjectReference to the
`Duplicator.Attributes` project, and the generator project will not have
any attribute defined. With that done, we can move to the next section.

> 📋 Side note: if there's only one consumer project for your generator,
> you can define the triggering attribute in the consuming project as well.
> In our case, this would bean moving the `DuplicateWithSuffixAttribute.cs`
> from `Duplicator` to `Reflector`, and adding a reference to the
> [`CodeGeneration.Roslyn.Attributes`][AttrNuPkg] in Reflector:
> > `dotnet add Reflector package CodeGeneration.Roslyn.Attributes`
> >
> > `mv Duplicator/DuplicateWithSuffixAttribute.cs Reflector`
> 
> For simplicity, we'll assume this is the case in the following sections.

### Customize generator reference

With the attribute available to consuming code, we don't need a reference to
the generator project, right? Well, not quite. The magic OutputItemType metadata
is important - it adds a path to the generator dll to the list of plugins known
to the `CodeGeneration.Roslyn.Tool` tool. Additionally, we want to specify that there's a build dependency of the consuming project on the generator. So we modify
the reference:

```xml
<!-- Reflector/Reflector.csproj -->
  <ItemGroup>
    <ProjectReference Include="..\Duplicator\Duplicator.csproj"
      ReferenceOutputAssembly="false"
      SkipGetTargetFrameworkProperties="true"
      OutputItemType="CodeGenerationRoslynPlugin" />
  </ItemGroup>
```

We add two new metadata attributes:
- `ReferenceOutputAssembly="false"` - this causes the compilation to not
  add a reference to Duplicator.dll to the `ReferencePath` - so the source
  code has no dependency and doesn't know anything about that project.
- `SkipGetTargetFrameworkProperties="true"` - this prevents build tasks
  from checking compatibility of the generator's TFM with this project's TFM.

#### Multitargeting generator

It can happen that your generator project will become multi-targeting. You could
need to do that to use C#8's Nullable Reference Types feature in the Duplicator;
the generator has to target `netcoreapp2.1` as this is the framework it'll be run
in by the `CG.R.Tool` - on the other hand, NRT feature is only supported in newer
TFMs, starting with `netcoreapp3.1`. So you'll do:
```xml
<!-- Duplicator/Duplicator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netcoreapp2.1;netcoreapp3.1</TargetFrameworks>
  </PropertyGroup>
  <!-- ... -->
</Project>
```

There'll be a build error, because the consumer (Reflector) doesn't know which
output to use (and assign to the CodeGenerationRoslynPlugin Item). To fix that
we have to use `SetTargetFramework` metadata. Setting it implies
`SkipGetTargetFrameworkProperties="true"` so we can replace it.

```xml
<!-- Reflector/Reflector.csproj -->
  <ItemGroup>
    <ProjectReference Include="..\Duplicator\Duplicator.csproj"
      ReferenceOutputAssembly="false"
      SetTargetFramework="TargetFramework=netcoreapp2.1"
      OutputItemType="CodeGenerationRoslynPlugin" />
  </ItemGroup>
```

### Package your code generator

You can also package up your code generator as a NuGet package for others to install
and use. A project using `CodeGeneration.Roslyn.Plugin.Sdk` is automatically
configured to produce a correct Plugin nuget package.

Your consumers will have to depend on the following:
- [`CodeGeneration.Roslyn.Tool`][ToolNuPkg] tool
- `Duplicator.Attributes` (your attributes package)
- `Duplicator` (your generator/plugin package)

An example consuming project file should contain:
```xml
<!-- Reflector/Reflector.csproj -->
<ItemGroup>
  <PackageReference Include="Duplicator" Version="1.0.0" PrivateAssets="all" />
  <PackageReference Include="Duplicator.Attributes" Version="1.0.0" PrivateAssets="all" />
  <PackageReference Include="CodeGeneration.Roslyn.Tool"
                    Version="{CodeGeneration.Roslyn.Tool version}"
                    PrivateAssets="all" />
</ItemGroup>
```

> 📋 You can also attempt to craft a self-contained package that will
> flow all the needed dependencies and assets into the consuming project.
> For a sample implementation, see [MetapackageSample](samples/MetapackageSample/).

[NuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn
[BuildTimeNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.BuildTime
[AttrNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.Attributes
[ToolNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.Tool
[netstandard-table]: https://docs.microsoft.com/dotnet/standard/net-standard#net-implementation-support
[MSBuild project SDK]: https://docs.microsoft.com/visualstudio/msbuild/how-to-use-project-sdk
