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
    - [Use template pack](#use-template-pack)
    - [Define code generator](#define-code-generator)
    - [Define attribute](#define-attribute)
    - [Create consuming console app](#create-consuming-console-app)
    - [Apply code generation](#apply-code-generation)
  - [Advanced scenarios](#advanced-scenarios)
    - [Customize generator reference](#customize-generator-reference)
      - [Multitargeting generator](#multitargeting-generator)
    - [Package your code generator](#package-your-code-generator)
      - [Separate out the attribute](#separate-out-the-attribute)
      - [Create the metapackage](#create-the-metapackage)
      - [Add extra `build/` content in Plugin package](#add-extra-build-content-in-plugin-package)

## How to write your own code generator

In this walkthrough, we will define a code generator that replicates any class (annotated with our custom attribute) with a suffix (specified in the attribute) appended to its name.

### Prerequisites

* [.NET Core SDK v2.1+][dotnet-sdk-2.1]
  
  If you don't have v2.1+ there will be cryptic error messages
  (see [#111](https://github.com/AArnott/CodeGeneration.Roslyn/issues/111)).

[dotnet-sdk-2.1]: https://dotnet.microsoft.com/download/dotnet-core/2.1

### Use template pack

To install the template pack, run:
> `dotnet new -i CodeGeneration.Roslyn.Templates`

You'll then have our template pack installed and ready for use with `dotnet new`.
For details see [templates Readme](./templates/README.md).

Prepare a directory where you want to create your Plugin projects, e.g. `mkdir DemoGeneration`.
Then, in that directory, create the set of Plugin projects (we add the --sln to create solution as well):
> `dotnet new cgrplugin -n Duplicator --sln`

This will create 3 ready-to-build projects. You can now skip through the next steps
of creating and setting up projects, just apply the following changes to have the same content:
- rename `Duplicator.Generators/Generator1.cs` to `DuplicateWithSuffixGenerator.cs`
  - also replace the `DuplicateWithSuffixGenerator` class with the following:
    ```csharp
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
    ```
- rename `Duplicator.Attributes/Generator1Attribute.cs` file to `DuplicateWithSuffixAttribute.cs`
  - also replace `DuplicateWithSuffixAttribute` class with the following:
    ```csharp
    [AttributeUsage(AttributeTargets.Class)]
    [CodeGenerationAttribute("Duplicator.Generators.DuplicateWithSuffixGenerator, Duplicator.Generators")]
    [Conditional("CodeGeneration")]
    public class DuplicateWithSuffixAttribute : Attribute
    {
        public DuplicateWithSuffixAttribute(string suffix)
        {
            Suffix = suffix;
        }

        public string Suffix { get; }
    }
    ```

### Define code generator

Your generator cannot be defined in the same project that will have code generated
for it. That's because code generation runs *before* the consuming project is itself compiled. We'll start by creating the generator. This must be done in a library that targets `netcoreapp2.1`. Let's create one called Duplicator:

> `dotnet new classlib -f netcoreapp2.1 -o Duplicator.Generators`

Now we'll use an [MSBuild project SDK] [`CodeGeneration.Roslyn.Plugin.Sdk`][PluginSdkNuPkg] to speed up configuring our generator plugin. Edit your project file and add the `<Sdk>` element:

```xml
<!-- Duplicator.Generators/Duplicator.Generators.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Add the following element above any others: -->
  <Sdk Name="CodeGeneration.Roslyn.Plugin.Sdk" Version="{replace with actual version used}" />

  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>
```

This SDK will implicitly add a PackageReference to the corresponding version of [`CodeGeneration.Roslyn`][NuPkg] nuget, and set properties to make plugin build correctly. We can now write our generator:

> ⚠ *Note: constructor is required to have exactly a single `AttributeData` parameter.*

```csharp
// Duplicator.Generators/DuplicateWithSuffixGenerator.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duplicator.Generators
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
we'll annotate the class to be copied. Let's do that in a new project:
> `dotnet new classlib -f netstandard2.0 -o Duplicator.Attributes`

Install [Attributes package][AttrNuPkg]:

> `dotnet add Duplicator.Attributes package CodeGeneration.Roslyn.Attributes`

Then, define your attribute class:

```csharp
// Duplicator.Attributes/DuplicateWithSuffixAttribute.cs
using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;

namespace Duplicator
{
    [AttributeUsage(AttributeTargets.Class)]
    [CodeGenerationAttribute("Duplicator.Generators.DuplicateWithSuffixGenerator, Duplicator.Generators")]
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

`CodeGenerationAttribute` is crucial - this tells the CG.R Tool which generator
to invoke for a member annotated with our `DuplicateWithSuffixAttribute`.
It's parameter is an assembly-qualified generator type name (incl. namespace):
`Full.Type.Name, Full.Assembly.Name`

The `[Conditional("CodeGeneration")]` attribute is not necessary, but it will prevent
the attribute from persisting in the compiled assembly that consumes it, leaving it
instead as just a compile-time hint to code generation, and allowing you to not ship
with a dependency on your code generation assembly.

> ℹ Of course, the attribute will persist if you define compilation symbol
> "CodeGeneration"; we assume it won't be defined.

### Create consuming console app

We'll consume our generator in a Reflector app:
> `dotnet new console -o Reflector`
> 
> `dotnet add Reflector reference Duplicator.Attributes`
> 
> `dotnet add Reflector reference Duplicator.Generators`

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
or member supported by the attribute and generator you wrote. We'll test our Duplicator on a new `Test` class:

```csharp
// Reflector/Program.cs
using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;
using Duplicator;

namespace Reflector
{
    [DuplicateWithSuffix("Passed")]
    class Test {}

    class Program
    {
        // ...
    }
}
```

Let's check our app again:
> `> dotnet run -p Reflector`
> 
> `Reflector.Program`
> 
> `Reflector.Test`

Still nothing except what we wrote. Now all that's left is to plumb the build pipeline with code generation tool; that tool will handle invoking our Duplicator
at correct time during build and write generated file, passing it into the
compilation.

You'll need to add a reference to [`CodeGeneration.Roslyn.Tool`][ToolNuPkg] package:
> `dotnet add Reflector package CodeGeneration.Roslyn.Tool`

Also, you need to add the following metadata to your generator project reference:
`OutputItemType="CodeGenerationRoslynPlugin"`. This will add the path to the `Duplicator.Generators.dll` to the list of plugins the tool runs.

This is how your project file can look like:

```xml
<!-- Reflector/Reflector.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- This ProjectReference to the generator needs to have the OutputItemType metadata -->
    <ProjectReference Include="..\Duplicator.Generators\Duplicator.Generators.csproj"
                      OutputItemType="CodeGenerationRoslynPlugin" />
    <PackageReference Include="CodeGeneration.Roslyn.Attributes"
                      Version="{replace with actual version used}" />
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

And if all steps were done correctly:
> `> dotnet run -p Reflector`
> 
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

### Customize generator reference

We don't need the generator as a compile reference. However, the magic OutputItemType
metadata is important - it adds a path to the generator dll to the list of plugins
run by the `CodeGeneration.Roslyn.Tool` tool. Additionally, we want to specify that
there's a build dependency of the consuming project on the generator. So we modify
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
- `ReferenceOutputAssembly="false"` - this causes the compilation to not add
  a reference to Duplicator.Generators.dll to the `ReferencePath` - so the source
  code has no dependency and doesn't know anything about that project.
- `SkipGetTargetFrameworkProperties="true"` - this prevents build tasks
  from checking compatibility of the generator's TFM with this project's TFM.

Now we can retarget our Reflector to any TFM compatible with Attributes package
(so `netstandard1.0`-compatible), e.g. netcoreapp2.0 - and it should run just fine.

#### Multitargeting generator

It can happen that your generator project will become multi-targeting. You could
need to do that to use C#8's Nullable Reference Types feature in the Duplicator;
the generator has to target `netcoreapp2.1` as this is the framework it'll be run
in by the `CG.R.Tool` - on the other hand, NRT feature is only supported in newer
TFMs, starting with `netcoreapp3.1`. So you'll do:
```xml
<!-- Duplicator.Generators/Duplicator.Generators.csproj -->
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
<Project Sdk="Microsoft.NET.Sdk">
  <!-- ... -->
  <ItemGroup>
    <ProjectReference Include="..\Duplicator\Duplicator.csproj"
      ReferenceOutputAssembly="false"
      SetTargetFramework="TargetFramework=netcoreapp2.1"
      OutputItemType="CodeGenerationRoslynPlugin" />
    <!-- ... -->
  </ItemGroup>
</Project>
```

### Package your code generator

> ℹ If you've used `cgrplugin` template, you've already got metapackage project ready.

You can also package up your code generator as a NuGet package for others to install
and use. A project using `CodeGeneration.Roslyn.Plugin.Sdk` is automatically
configured to produce a correct Plugin nuget package.

#### Separate out the attribute

> ⚠ This section is deprecated since it's now the default.

The triggering attribute has to be available in consuming code. Your consumers
can write it themselves, but it's not a good idea to require them do so.
So we'll separate the attribute into another project that has TFM allowing
all your consumers to reference it, for example `netstandard1.0` - you're
constrained only by what [`CodeGeneration.Roslyn.Attributes`][AttrNuPkg] targets.

Let's call this new project `Duplicator.Attributes`.
> `dotnet new classlib -o Duplicator.Attributes`
> `dotnet add Duplicator.Attributes package CodeGeneration.Roslyn.Attributes`
> `dotnet add Reflector reference Duplicator.Attributes`

Now, move the attribute definition from Reflector to our new project. Dont' forget
to change the namespace. The app should work the same, except for not printing
`DuplicateWithSuffixAttribute` type.

If you annotate your triggering attribute with `[Conditional("...")]`,
your consumers can make any references to the attribute package non-transient
via `PrivateAssets="all"` and/or exclude from runtime assets via `ExcludeAssets="runtime"`.

#### Create the metapackage

Your consumers will now have to depend (have `PackageReference`) on the following:
- [`CodeGeneration.Roslyn.Tool`][ToolNuPkg] tool
- `Duplicator.Attributes` (your attributes package)
- `Duplicator.Generators` (your generator/plugin package)

An example consuming project file would contain:
```xml
<!-- Reflector/Reflector.csproj -->
<ItemGroup>
  <PackageReference Include="Duplicator.Generators" Version="1.0.0" PrivateAssets="all" />
  <PackageReference Include="Duplicator.Attributes" Version="1.0.0" PrivateAssets="all" />
  <PackageReference Include="CodeGeneration.Roslyn.Tool"
                    Version="{CodeGeneration.Roslyn.Tool version}"
                    PrivateAssets="all" />
</ItemGroup>
```

> ✔ There's a much better approach: **metapackage**.

For this, we'll use [`CodeGeneration.Roslyn.PluginMetapackage.Sdk`][PluginMetapkgSdkNuPkg] [MSBuild project SDK]
in a new project called simply `Duplicator`, which will reference our attributes:
> `dotnet new classlib -o Duplicator`
> `dotnet add Duplicator reference Duplicator.Attributes`

Remove `Class1.cs` file.

Modify project file:
- Add `<Sdk>` element
- Set `NoBuild=true` and `IncludeBuildOutput=false`
- Add `NupkgAdditionalDependency` in ItemGroup
```xml
<!-- Duplicator/Duplicator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Add the following element above any others: -->
  <Sdk Name="CodeGeneration.Roslyn.PluginMetapackage.Sdk" Version="{replace with actual version used}"/>

  <PropertyGroup>
    <!-- Declare the TargetFramework(s) the same as in your Attributes package -->
    <TargetFramework>netstandard1.0</TargetFramework>
    <!-- This project contains no files, so building can be skipped -->
    <NoBuild>true</NoBuild>
    <!-- Since we don't build, there'll be no build output -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference your Attributes project normally -->
    <ProjectReference Include="../Duplicator.Attributes/Duplicator.Attributes.csproj" />
    <!--
      Reference your generators package by adding an item to NupkgAdditionalDependency
      with IncludeAssets="all" to flow "build" assets.
      Version used will be the PackageVersion Pack resolves,
      but you can specify Version metadata to override it.

      This is necessary to do like that, because it ensures the dependency is setup
      correctly (e.g. simple transient dependency), and skips validation of TFM (Plugin is a tool,
      it's TFM has no meaning for the consumer).
    -->
    <NupkgAdditionalDependency
        Include="Duplicator.Generators"
        IncludeAssets="all" />
  </ItemGroup>
</Project>
```

This project will now produce a `nupkg` that will not contain
anything on it's own, but will "pull in" all the other dependecies
consumers will need:
- `CodeGeneration.Roslyn.Tool` (added implicitly by the Sdk)
- `Duplicator.Attributes`
- `Duplicator.Generators`

Our metapackage should be versioned in the same manner
as it's dependant packages.

> 📋 For a sample metapackage, see [MetapackageSample](samples/MetapackageSample/).


#### Add extra `build/` content in Plugin package

`CG.R.Plugin.Sdk` creates custom `build/PackageId.props/targets` files. If you want
to add custom MSBuild props/targets into NuGet package's `build` folder (and have it
imported when package is referenced), you'll need to use `PackageBuildFolderProjectImport`
ItemGroup, as shown in `PackagedGenerator` sample.

[NuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn
[AttrNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.Attributes
[ToolNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.Tool
[PluginSdkNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.Plugin.Sdk
[PluginMetapkgSdkNuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn.PluginMetapackage.Sdk
[netstandard-table]: https://docs.microsoft.com/dotnet/standard/net-standard#net-implementation-support
[MSBuild project SDK]: https://docs.microsoft.com/visualstudio/msbuild/how-to-use-project-sdk
