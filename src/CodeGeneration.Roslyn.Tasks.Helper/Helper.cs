// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
#if NETCOREAPP1_0
    using System.Runtime.Loader;
#endif
    using System.Text;
    using System.Threading;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Task = System.Threading.Tasks.Task;
    using Validation;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public class Helper
#if NET46
        : MarshalByRefObject
#endif
    {
        private readonly List<string> loadedAssemblies = new List<string>();

        private readonly CancellationTokenSource cts = new CancellationTokenSource();

#if NETCOREAPP1_0
        private readonly AssemblyLoadContext loadContext;

        public Helper(AssemblyLoadContext loadContext)
        {
            Requires.NotNull(loadContext, nameof(loadContext));
            this.loadContext = loadContext;
        }
#endif

        public CancellationToken CancellationToken => this.cts.Token;

        public ITaskItem[] Compile { get; set; }

        public string TargetName { get; set; }

        public ITaskItem[] ReferencePath { get; set; }

        public ITaskItem[] GeneratorAssemblySearchPaths { get; set; }

        public string IntermediateOutputDirectory { get; set; }

        public ITaskItem[] GeneratedCompile { get; set; }

        public ITaskItem[] AdditionalWrittenFiles { get; set; }

        public TaskLoggingHelper Log { get; set; }

        public void Execute()
        {
#if NET46
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                return this.TryLoadAssembly(new AssemblyName(e.Name));
            };
#else
            this.loadContext.Resolving += (lc, an) =>
            {
                Assumes.True(ReferenceEquals(lc, this.loadContext));
                return this.TryLoadAssembly(an);
            };
#endif

            Task.Run(async delegate
            {
                var compilation = this.CreateCompilation();
                var outputFiles = new List<ITaskItem>();
                var writtenFiles = new List<ITaskItem>();

                string generatorAssemblyInputsFile = Path.Combine(this.IntermediateOutputDirectory, "CodeGeneration.Roslyn.InputAssemblies.txt");

                // For incremental build, we want to consider the input->output files as well as the assemblies involved in code generation.
                DateTime assembliesLastModified = GetLastModifiedAssemblyTime(generatorAssemblyInputsFile);

                var explicitIncludeList = new HashSet<string>(
                    from item in this.Compile
                    where string.Equals(item.GetMetadata("Generator"), $"MSBuild:{this.TargetName}", StringComparison.OrdinalIgnoreCase)
                    select item.ItemSpec,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var inputSyntaxTree in compilation.SyntaxTrees)
                {
                    this.CancellationToken.ThrowIfCancellationRequested();

                    // Skip over documents that aren't on the prescribed list of files to scan.
                    if (!explicitIncludeList.Contains(inputSyntaxTree.FilePath))
                    {
                        continue;
                    }

                    string sourceHash = inputSyntaxTree.FilePath.GetHashCode().ToString("x", CultureInfo.InvariantCulture);
                    string outputFilePath = Path.Combine(this.IntermediateOutputDirectory, Path.GetFileNameWithoutExtension(inputSyntaxTree.FilePath) + $".{sourceHash}.generated.cs");

                    // Code generation is relatively fast, but it's not free.
                    // And when we run the Simplifier.ReduceAsync it's dog slow.
                    // So skip files that haven't changed since we last generated them.
                    bool generated = false;
                    DateTime outputLastModified = File.Exists(outputFilePath) ? File.GetLastWriteTime(outputFilePath) : DateTime.MinValue;
                    if (File.GetLastWriteTime(inputSyntaxTree.FilePath) > outputLastModified || assembliesLastModified > outputLastModified)
                    {
                        var generatedSyntaxTree = await DocumentTransform.TransformAsync(
                            compilation,
                            inputSyntaxTree,
                            new ProgressLogger(this.Log, inputSyntaxTree.FilePath));

                        // Only produce a new file if the generated document has generated a type.
                        bool anyMembersGenerated = generatedSyntaxTree?.GetRoot(this.CancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>().Any() ?? false;
                        if (anyMembersGenerated)
                        {
                            var outputText = generatedSyntaxTree.GetText(this.CancellationToken);
                            using (var outputFileStream = File.OpenWrite(outputFilePath))
                            using (var outputWriter = new StreamWriter(outputFileStream))
                            {
                                outputText.Write(outputWriter);

                                // Truncate any data that may be beyond this point if the file existed previously.
                                outputWriter.Flush();
                                outputFileStream.SetLength(outputFileStream.Position);
                            }

                            this.Log.LogMessage(MessageImportance.Normal, "{0} -> {1}", inputSyntaxTree.FilePath, outputFilePath);
                            generated = true;
                        }
                        else
                        {
                            this.Log.LogMessage(MessageImportance.Low, "{0} used no code generation attributes.", inputSyntaxTree.FilePath);
                        }
                    }
                    else
                    {
                        generated = true;
                    }

                    if (generated)
                    {
                        var outputItem = new TaskItem(outputFilePath);
                        outputFiles.Add(outputItem);
                    }
                }

                this.SaveGeneratorAssemblyList(generatorAssemblyInputsFile);
                writtenFiles.Add(new TaskItem(generatorAssemblyInputsFile));

                this.GeneratedCompile = outputFiles.ToArray();
                this.AdditionalWrittenFiles = writtenFiles.ToArray();
            }).GetAwaiter().GetResult();
        }

        public void Cancel() => this.cts.Cancel();

        private static DateTime GetLastModifiedAssemblyTime(string assemblyListPath)
        {
            if (!File.Exists(assemblyListPath))
            {
                return DateTime.MinValue;
            }

            var timestamps = (from path in File.ReadAllLines(assemblyListPath)
                              where File.Exists(path)
                              select File.GetLastWriteTime(path)).ToList();
            return timestamps.Any() ? timestamps.Max() : DateTime.MinValue;
        }

        private void SaveGeneratorAssemblyList(string assemblyListPath)
        {
            // Union our current list with the one on disk, since our incremental code generation
            // may have skipped some up-to-date files, resulting in fewer assemblies being loaded
            // this time.
            var assemblyPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(assemblyListPath))
            {
                assemblyPaths.UnionWith(File.ReadAllLines(assemblyListPath));
            }

            assemblyPaths.UnionWith(this.loadedAssemblies);

#if NET46
                assemblyPaths.UnionWith(
                    from a in AppDomain.CurrentDomain.GetAssemblies()
                    where !a.IsDynamic
                    select a.Location);
#endif

            File.WriteAllLines(
                assemblyListPath,
                assemblyPaths);
        }

        private Assembly LoadAssemblyByFile(string path)
        {
#if NET46
            return Assembly.LoadFile(path);
#else
            return this.loadContext.LoadFromAssemblyPath(path);
#endif
        }

        private Assembly TryLoadAssembly(AssemblyName assemblyName)
        {
            try
            {
                var referencePath = this.ReferencePath.FirstOrDefault(rp => string.Equals(rp.GetMetadata("FileName"), assemblyName.Name, StringComparison.OrdinalIgnoreCase));
                if (referencePath != null)
                {
                    string fullPath = referencePath.GetMetadata("FullPath");
                    this.loadedAssemblies.Add(fullPath);
                    return this.LoadAssemblyByFile(fullPath);
                }

                foreach (var searchPath in this.GeneratorAssemblySearchPaths)
                {
                    string searchDir = searchPath.GetMetadata("FullPath");
                    const string extension = ".dll";
                    string fileName = Path.Combine(searchDir, assemblyName.Name + extension);
                    if (File.Exists(fileName))
                    {
                        return LoadAssemblyByFile(fileName);
                    }
                }
            }
            catch (BadImageFormatException)
            {
            }

            return null;
        }

        private CSharpCompilation CreateCompilation()
        {
            var compilation = CSharpCompilation.Create("codegen")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(this.ReferencePath.Select(p => MetadataReference.CreateFromFile(p.ItemSpec)));
            foreach (var sourceFile in this.Compile)
            {
                using (var stream = File.OpenRead(sourceFile.GetMetadata("FullPath")))
                {
                    this.CancellationToken.ThrowIfCancellationRequested();
                    var text = SourceText.From(stream);
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(text, path: sourceFile.ItemSpec, cancellationToken: this.CancellationToken));
                }
            }

            return compilation;
        }
    }

    internal class ProgressLogger : IProgress<Diagnostic>
    {
        private readonly TaskLoggingHelper logger;
        private readonly string inputFilename;

        internal ProgressLogger(TaskLoggingHelper logger, string inputFilename)
        {
            this.logger = logger;
            this.inputFilename = inputFilename;
        }

        public void Report(Diagnostic value)
        {
            var lineSpan = value.Location.GetLineSpan();
            switch (value.Severity)
            {
                case DiagnosticSeverity.Info:
                    this.logger.LogMessage(
                        value.Descriptor.Category,
                        value.Descriptor.Id,
                        value.Descriptor.HelpLinkUri,
                        value.Location.SourceTree.FilePath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1,
                        lineSpan.EndLinePosition.Line + 1,
                        lineSpan.EndLinePosition.Character + 1,
                        MessageImportance.Normal,
                        value.GetMessage(CultureInfo.CurrentCulture));
                    break;
                case DiagnosticSeverity.Warning:
                    this.logger.LogWarning(
                        value.Descriptor.Category,
                        value.Descriptor.Id,
                        value.Descriptor.HelpLinkUri,
                        value.Location.SourceTree.FilePath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1,
                        lineSpan.EndLinePosition.Line + 1,
                        lineSpan.EndLinePosition.Character + 1,
                        value.GetMessage(CultureInfo.CurrentCulture));
                    break;
                case DiagnosticSeverity.Error:
                    this.logger.LogError(
                        value.Descriptor.Category,
                        value.Descriptor.Id,
                        value.Descriptor.HelpLinkUri,
                        value.Location.SourceTree.FilePath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1,
                        lineSpan.EndLinePosition.Line + 1,
                        lineSpan.EndLinePosition.Character + 1,
                        value.GetMessage(CultureInfo.CurrentCulture));
                    break;
                default:
                    break;
            }
        }
    }
}
