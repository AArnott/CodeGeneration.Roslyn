// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeGeneration.Roslyn;
using CodeGeneration.Roslyn.Engine;
using CodeGeneration.Roslyn.Tests.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

public abstract class CompilationTestsBase
{
    static CompilationTestsBase()
    {
        // this "core assemblies hack" is from https://stackoverflow.com/a/47196516/4418060
        var coreAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var coreAssemblyNames = new[]
        {
            "mscorlib.dll",
            "netstandard.dll",
            "System.dll",
            "System.Core.dll",
#if NETCOREAPP
            "System.Private.CoreLib.dll",
#endif
            "System.Runtime.dll",
        };
        var coreMetaReferences =
            coreAssemblyNames.Select(x => MetadataReference.CreateFromFile(Path.Combine(coreAssemblyPath, x)));
        var otherAssemblies = new[]
        {
            typeof(CSharpCompilation).Assembly,
            typeof(CodeGenerationAttributeAttribute).Assembly,
            typeof(TestAttribute).Assembly,
        };
        MetadataReferences = coreMetaReferences
            .Concat<MetadataReference>(otherAssemblies.Select(x => MetadataReference.CreateFromFile(x.Location)))
            .ToImmutableArray();
    }

    internal const string CrLf = "\r\n";
    internal const string Lf = "\n";
    internal const string DefaultFilePathPrefix = "Test";
    internal const string CSharpDefaultFileExt = "cs";
    internal const string TestProjectName = "TestProject";

    internal static readonly string NormalizedPreamble = NormalizeToLf(DocumentTransform.GeneratedByAToolPreamble + Lf);

    internal static readonly ImmutableArray<MetadataReference> MetadataReferences;

    protected static void AssertGeneratedAsExpected(string source, string expected)
    {
        var generatedTree = Generate(source);
        // normalize line endings to just LF
        var generatedText = NormalizeToLf(generatedTree.GetText().ToString());
        // and append preamble to the expected
        var expectedText = NormalizedPreamble + NormalizeToLf(expected).Trim();
        Assert.Equal(expectedText, generatedText);
    }

    protected static string NormalizeToLf(string input)
    {
        return input?.Replace(CrLf, Lf);
    }

    protected static SyntaxTree Generate(string source)
    {
        var document = CreateProject(source).Documents.Single();
        var tree = document.GetSyntaxTreeAsync().GetAwaiter().GetResult();
        var compilation = (CSharpCompilation)document.Project.GetCompilationAsync().GetAwaiter().GetResult();
        var diagnostics = compilation.GetDiagnostics();
        Assert.Empty(diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Warning));
        var progress = new Progress<Diagnostic>();
        var result = DocumentTransform.TransformAsync(compilation, tree, null, Assembly.Load, progress).GetAwaiter().GetResult();
        return result;
    }

    protected static Project CreateProject(params string[] sources)
    {
        var projectId = ProjectId.CreateNewId(debugName: TestProjectName);
        var solution = new AdhocWorkspace()
            .CurrentSolution
            .AddProject(projectId, TestProjectName, TestProjectName, LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(
                projectId,
                new CSharpParseOptions(preprocessorSymbols: new[] { "SOMETHING_ACTIVE" }))
            .AddMetadataReferences(projectId, MetadataReferences);

        int count = 0;
        foreach (var source in sources)
        {
            var newFileName = DefaultFilePathPrefix + count + "." + CSharpDefaultFileExt;
            var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
            solution = solution.AddDocument(documentId, newFileName, SourceText.From(source));
            count++;
        }
        return solution.GetProject(projectId);
    }
}
