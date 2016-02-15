# Roslyn-based Code Generation

[![Build status](https://ci.appveyor.com/api/projects/status/y81yha5rm3wvlycv/branch/master?svg=true)](https://ci.appveyor.com/project/AArnott/codegeneration-roslyn/branch/master)
[![NuGet package](https://img.shields.io/nuget/v/CodeGeneration.Roslyn.svg)][NuPkg]

Assists in performing Roslyn-based code generation during a build.
This includes design-time support, such that code generation can respond to
changes made in hand-authored code files by generating new code that shows
up to Intellisense as soon as the file is saved to disk.

## Installation

Install the [CodeGeneration.Roslyn NuGet package][NuPkg].

## Define code generation

To write your code that generates more code, after installing the package,
define two classes. First, an attribute class. This is the attribute your
users will affix to a type or member to produce more code based on it.
That attribute will refer to another class which actually performs the
code generation.

In the following example, we define a pair of classes which exactly duplicates
any class the attribute is applied to, but adds some suffix to the name on the copy.

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
[CodeGenerationAttribute(typeof(DuplicateWithSuffixGenerator))]
[Conditional("CodeGeneration")]
public class DuplicateWithSuffixAttribute : Attribute
{
    public DuplicateWithSuffixAttribute(string suffix)
    {
        Requires.NotNullOrEmpty(suffix, nameof(suffix));

        this.Suffix = suffix;
    }

    public string Suffix { get; }
}

public class DuplicateWithSuffixGenerator : ICodeGenerator
{
    private readonly string suffix;

    public DuplicateWithSuffixGenerator(AttributeData attributeData)
    {
        Requires.NotNull(attributeData, nameof(attributeData));

        this.suffix = (string)attributeData.ConstructorArguments[0].Value;
    }

    public Task<IReadOnlyList<MemberDeclarationSyntax>> GenerateAsync(MemberDeclarationSyntax applyTo, Document document, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
    {
        var results = new List<MemberDeclarationSyntax>();

        var applyToClass = (ClassDeclarationSyntax)applyTo;
        var copy = applyToClass
            .WithIdentifier(SyntaxFactory.Identifier(applyToClass.Identifier.ValueText + this.suffix));
        results.Add(copy);

        return Task.FromResult<IReadOnlyList<MemberDeclarationSyntax>>(results);
    }
}
```

The `[Conditional("CodeGeneration")]` attribute is not necessary, but it will prevent
the attribute from persisting in the compiled assembly that consumes it, leaving it
instead as just a compile-time hint to code generation, and allowing you to not ship
with a dependency on your code generation assembly.

## Apply code generation

The attribute may not be applied in the same assembly that defines the generator.
This is because the code generator must be compiled in order to execute before compiling
the project that applies the attribute.

Applying code generation is incredibly simple. Just add the attribute on any type
or member supported by the attribute and generator you wrote:

```csharp
[DuplicateWithSuffix("A")]
public class Foo
{
}
```

Any file that uses this attribute should have its Custom Tool property set to
`MSBuild:GenerateCodeFromAttributes` so that when you save the file or compile
the project, the code generator runs and acts on your attribute.

You can then consume the generated code at design-time:

```csharp
[Fact]
public void SimpleGenerationWorks()
{
    var foo = new Foo();
    var fooA = new FooA();
}
```

You should see Intellisense help you in all your interactions with `FooA`.
If you execute Go To Definition on it, Visual Studio will open the generated code file
that actually defines `FooA`, and you'll notice it's exactly like `Foo`, just renamed
as our code generator defined it to be.

### Shared Projects

When using shared projects and partial classes across the definitions of your class in shared and platform projects:

* The code generation attributes should be applied only to the files in the shared project
  (or in other words, the attribute should only be applied once per type to avoid multiple generator invocations).
* The MSBuild:GenerateCodeFromAttributes custom tool must be applied to every file we want to auto generate code from.

## Developing your code generator

Your code generator can be defined in a project in the same solution as the solution with
the project that consumes it. You can edit your code generator and build the solution
to immediately see the effects of your changes on the generated code.

## Packaging up your code generator for others' use

You can also package up your code generator as a NuGet package for others to install
and use. But the mechanism for this is currently rather tedious as you have to pack
many files into your NuGet package's `tools` folder. This experience is expected to
improve in the future, if there is demand for it.

[NuPkg]: https://nuget.org/packages/CodeGeneration.Roslyn
