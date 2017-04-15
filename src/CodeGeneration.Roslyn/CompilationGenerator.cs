// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using Validation;

    public class CompilationGenerator
    {
        private const string InputAssembliesIntermediateOutputFileName = "CodeGeneration.Roslyn.InputAssemblies.txt";
        private static readonly HashSet<string> AllowedAssemblyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll" };

        private readonly List<string> emptyGeneratedFiles = new List<string>();
        private readonly List<string> generatedFiles = new List<string>();
        private readonly List<string> additionalWrittenFiles = new List<string>();
        private readonly List<string> loadedAssemblies = new List<string>();

        /// <summary>
        /// Gets or sets the list of paths of files to be compiled.
        /// </summary>
        public IReadOnlyList<string> Compile { get; set; }

        /// <summary>
        /// Gets or sets the list of paths to reference assemblies.
        /// </summary>
        public IReadOnlyList<string> ReferencePath { get; set; }

        /// <summary>
        /// Gets or sets the paths to directories to search for generator assemblies.
        /// </summary>
        public IReadOnlyList<string> GeneratorAssemblySearchPaths { get; set; }

        /// <summary>
        /// Gets or sets the path to the directory that contains generated source files.
        /// </summary>
        public string IntermediateOutputDirectory { get; set; }

        /// <summary>
        /// Gets the set of files generated after <see cref="Generate"/> is invoked.
        /// </summary>
        public IEnumerable<string> GeneratedFiles => this.generatedFiles;

        /// <summary>
        /// Gets the set of files written in addition to those found in <see cref="GeneratedFiles"/>.
        /// </summary>
        public IEnumerable<string> AdditionalWrittenFiles => this.additionalWrittenFiles;

        /// <summary>
        /// Gets the subset of <see cref="GeneratedFiles"/> that contain no types.
        /// </summary>
        public IEnumerable<string> EmptyGeneratedFiles => this.emptyGeneratedFiles;

        public void Generate(IProgress<Diagnostic> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Verify.Operation(this.Compile != null, $"{nameof(Compile)} must be set first.");
            Verify.Operation(this.ReferencePath != null, $"{nameof(ReferencePath)} must be set first.");
            Verify.Operation(this.IntermediateOutputDirectory != null, $"{nameof(IntermediateOutputDirectory)} must be set first.");
            Verify.Operation(this.GeneratorAssemblySearchPaths != null, $"{nameof(GeneratorAssemblySearchPaths)} must be set first.");

            var compilation = this.CreateCompilation(cancellationToken);

            string generatorAssemblyInputsFile = Path.Combine(this.IntermediateOutputDirectory, InputAssembliesIntermediateOutputFileName);

            // For incremental build, we want to consider the input->output files as well as the assemblies involved in code generation.
            DateTime assembliesLastModified = GetLastModifiedAssemblyTime(generatorAssemblyInputsFile);

            using (var hasher = System.Security.Cryptography.SHA1.Create())
            {
                foreach (var inputSyntaxTree in compilation.SyntaxTrees)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string sourceHash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(inputSyntaxTree.FilePath)), 0, 6).Replace('/', '-');
                    Console.WriteLine($"File \"{inputSyntaxTree.FilePath}\" hashed to {sourceHash}");
                    string outputFilePath = Path.Combine(this.IntermediateOutputDirectory, Path.GetFileNameWithoutExtension(inputSyntaxTree.FilePath) + $".{sourceHash}.generated.cs");

                    // Code generation is relatively fast, but it's not free.
                    // So skip files that haven't changed since we last generated them.
                    DateTime outputLastModified = File.Exists(outputFilePath) ? File.GetLastWriteTime(outputFilePath) : DateTime.MinValue;
                    if (File.GetLastWriteTime(inputSyntaxTree.FilePath) > outputLastModified || assembliesLastModified > outputLastModified)
                    {
                        var generatedSyntaxTree = DocumentTransform.TransformAsync(
                            compilation,
                            inputSyntaxTree,
                            this.LoadAssembly,
                            progress).GetAwaiter().GetResult();

                        var outputText = generatedSyntaxTree.GetText(cancellationToken);
                        using (var outputFileStream = File.OpenWrite(outputFilePath))
                        using (var outputWriter = new StreamWriter(outputFileStream))
                        {
                            outputText.Write(outputWriter);

                            // Truncate any data that may be beyond this point if the file existed previously.
                            outputWriter.Flush();
                            outputFileStream.SetLength(outputFileStream.Position);
                        }

                        bool anyTypesGenerated = generatedSyntaxTree?.GetRoot(cancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>().Any() ?? false;
                        if (anyTypesGenerated)
                        {
                            this.emptyGeneratedFiles.Add(outputFilePath);
                        }
                    }


                    this.generatedFiles.Add(outputFilePath);
                }
            }

            this.SaveGeneratorAssemblyList(generatorAssemblyInputsFile);
        }

        protected virtual Assembly LoadAssembly(string path)
        {
            var loadContext = AssemblyLoadContext.GetLoadContext(this.GetType().GetTypeInfo().Assembly);
            return loadContext.LoadFromAssemblyPath(path);
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

        private Assembly LoadAssembly(AssemblyName assemblyName)
        {
            var matchingRefAssemblies = from refPath in this.ReferencePath
                                        where Path.GetFileNameWithoutExtension(refPath).Equals(assemblyName.Name, StringComparison.OrdinalIgnoreCase)
                                        select refPath;
            var matchingAssemblies = from path in this.GeneratorAssemblySearchPaths
                                     from file in Directory.EnumerateFiles(path, $"{assemblyName.Name}.*", SearchOption.TopDirectoryOnly)
                                     where AllowedAssemblyExtensions.Contains(Path.GetExtension(file))
                                     select file;

            string matchingRefAssembly = matchingRefAssemblies.Concat(matchingAssemblies).FirstOrDefault();
            if (matchingRefAssembly != null)
            {
                this.loadedAssemblies.Add(matchingRefAssembly);
                return this.LoadAssembly(matchingRefAssembly);
            }

            return Assembly.Load(assemblyName);
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

            File.WriteAllLines(assemblyListPath, assemblyPaths);
        }

        private CSharpCompilation CreateCompilation(CancellationToken cancellationToken)
        {
            var compilation = CSharpCompilation.Create("codegen")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(this.ReferencePath.Select(p => MetadataReference.CreateFromFile(p)));
            foreach (var sourceFile in this.Compile)
            {
                using (var stream = File.OpenRead(sourceFile))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = SourceText.From(stream);
                    compilation = compilation.AddSyntaxTrees(
                        CSharpSyntaxTree.ParseText(
                            text,
                            path: sourceFile,
                            cancellationToken: cancellationToken));
                }
            }

            return compilation;
        }
    }
}
