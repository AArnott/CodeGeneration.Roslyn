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
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.CodeAnalysis;

    public class GenerateCodeFromAttributes : Nerdbank.MSBuildExtension.ContextIsolatedTask
    {
        private readonly List<string> loadedAssemblies = new List<string>();

        [Required]
        public ITaskItem[] Compile { get; set; }

        /// <summary>
        /// Gets or sets the name of the target running code generation.
        /// </summary>
        public string TargetName { get; set; }

        [Required]
        public ITaskItem[] ReferencePath { get; set; }

        [Required]
        public ITaskItem[] GeneratorAssemblySearchPaths { get; set; }

        [Required]
        public string IntermediateOutputDirectory { get; set; }

        [Output]
        public ITaskItem[] GeneratedCompile { get; set; }

        [Output]
        public ITaskItem[] AdditionalWrittenFiles { get; set; }

        protected override bool ExecuteIsolated()
        {
            //System.Diagnostics.Debugger.Launch();
            return ExecuteIsolatedInner();
        }

        private bool ExecuteIsolatedInner()
        {
#if NET46
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                return this.TryLoadAssembly(new AssemblyName(e.Name));
            };
#else
            AssemblyLoadContext.GetLoadContext(this.GetType().GetTypeInfo().Assembly).Resolving += (lc, an) =>
            {
                return this.TryLoadAssembly(an);
            };
#endif

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
                // So skip files that haven't changed since we last generated them.
                DateTime outputLastModified = File.Exists(outputFilePath) ? File.GetLastWriteTime(outputFilePath) : DateTime.MinValue;
                if (File.GetLastWriteTime(inputSyntaxTree.FilePath) > outputLastModified || assembliesLastModified > outputLastModified)
                {
                    var generatedSyntaxTree = DocumentTransform.TransformAsync(
                        compilation,
                        inputSyntaxTree,
                        new ProgressLogger(this.Log, inputSyntaxTree.FilePath)).GetAwaiter().GetResult();

                    var outputText = generatedSyntaxTree.GetText(this.CancellationToken);
                    using (var outputFileStream = File.OpenWrite(outputFilePath))
                    using (var outputWriter = new StreamWriter(outputFileStream))
                    {
                        outputText.Write(outputWriter);

                        // Truncate any data that may be beyond this point if the file existed previously.
                        outputWriter.Flush();
                        outputFileStream.SetLength(outputFileStream.Position);
                    }

                    bool anyTypesGenerated = generatedSyntaxTree?.GetRoot(this.CancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>().Any() ?? false;
                    if (anyTypesGenerated)
                    {
                        this.Log.LogMessage(MessageImportance.Normal, "{0} -> {1}", inputSyntaxTree.FilePath, outputFilePath);
                    }
                    else
                    {
                        this.Log.LogMessage(MessageImportance.Low, "{0} used no code generation attributes.", inputSyntaxTree.FilePath);
                    }
                }

                var outputItem = new TaskItem(outputFilePath);
                outputFiles.Add(outputItem);
            }

            this.SaveGeneratorAssemblyList(generatorAssemblyInputsFile);
            writtenFiles.Add(new TaskItem(generatorAssemblyInputsFile));

            this.GeneratedCompile = outputFiles.ToArray();
            this.AdditionalWrittenFiles = writtenFiles.ToArray();

            return !this.Log.HasLoggedErrors;
        }

        protected override Assembly LoadAssemblyByName(AssemblyName assemblyName)
        {
            try
            {
                if (!assemblyName.Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) && !assemblyName.Name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
                {
                    var referencePath = this.ReferencePath.FirstOrDefault(rp => string.Equals(rp.GetMetadata("FileName"), assemblyName.Name, StringComparison.OrdinalIgnoreCase));
                    if (referencePath != null)
                    {
                        string fullPath = referencePath.GetMetadata("FullPath");
                        this.loadedAssemblies.Add(fullPath);
                        return this.LoadAssemblyByPath(fullPath);
                    }
                }

                foreach (var searchPath in this.GeneratorAssemblySearchPaths)
                {
                    string searchDir = searchPath.GetMetadata("FullPath");
                    const string extension = ".dll";
                    string fileName = Path.Combine(searchDir, assemblyName.Name + extension);
                    if (File.Exists(fileName))
                    {
                        return this.LoadAssemblyByPath(fileName);
                    }
                }
            }
            catch (BadImageFormatException)
            {
            }

            return base.LoadAssemblyByName(assemblyName);
        }

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

        private Assembly TryLoadAssembly(AssemblyName assemblyName)
        {
            try
            {
                var referencePath = this.ReferencePath.FirstOrDefault(rp => string.Equals(rp.GetMetadata("FileName"), assemblyName.Name, StringComparison.OrdinalIgnoreCase));
                if (referencePath != null)
                {
                    string fullPath = referencePath.GetMetadata("FullPath");
                    this.loadedAssemblies.Add(fullPath);
                    return this.LoadAssemblyByPath(fullPath);
                }

                foreach (var searchPath in this.GeneratorAssemblySearchPaths)
                {
                    string searchDir = searchPath.GetMetadata("FullPath");
                    const string extension = ".dll";
                    string fileName = Path.Combine(searchDir, assemblyName.Name + extension);
                    if (File.Exists(fileName))
                    {
                        return LoadAssemblyByPath(fileName);
                    }
                }
            }
            catch (BadImageFormatException)
            {
            }

            return null;
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
}
