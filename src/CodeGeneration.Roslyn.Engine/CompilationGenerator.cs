// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Engine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using McMaster.NETCore.Plugins;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using Validation;

    /// <summary>
    /// Runs code generation for every applicable document and handles resulting syntax trees,
    /// saving them to <see cref="IntermediateOutputDirectory"/>.
    /// </summary>
    public class CompilationGenerator
    {
        private const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);

        private readonly Type[] pluginSharedTypes = new[] { typeof(ICodeGenerator), typeof(Compilation), typeof(CSharpCompilation) };
        private readonly List<string> emptyGeneratedFiles = new List<string>();
        private readonly List<string> generatedFiles = new List<string>();
        private readonly List<string> additionalWrittenFiles = new List<string>();
        private readonly List<string> loadedAssemblies = new List<string>();
        private readonly Dictionary<string, (PluginLoader loader, Assembly assembly)> cachedPlugins = new Dictionary<string, (PluginLoader, Assembly)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the list of paths of files to be compiled.
        /// </summary>
        public IReadOnlyList<string> Compile { get; set; }

        /// <summary>
        /// Gets or sets the list of paths to reference assemblies.
        /// </summary>
        public IReadOnlyList<string> ReferencePath { get; set; }

        /// <summary>
        /// Gets or sets a set of preprocessor symbols to define.
        /// </summary>
        public IEnumerable<string> PreprocessorSymbols { get; set; }

        /// <summary>
        /// Gets or sets the paths to plugins.
        /// </summary>
        public IReadOnlyList<string> PluginPaths { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the path to the directory that contains generated source files.
        /// </summary>
        public string IntermediateOutputDirectory { get; set; }

        /// <summary>
        /// Gets the set of files generated after <see cref="GenerateAsync"/> is invoked.
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

        /// <summary>
        /// Gets or sets the directory with the project file.
        /// </summary>
        public string ProjectDirectory { get; set; }

        /// <summary>
        /// Runs the code generation as configured using this instance's properties.
        /// </summary>
        /// <param name="progress">Optional handler of diagnostics provided by code generator.</param>
        /// <param name="cancellationToken">Cancellation token to interrupt async operations.</param>
        /// <returns>A <see cref="Task.CompletedTask"/>.</returns>
        public async Task GenerateAsync(IProgress<Diagnostic> progress = null, CancellationToken cancellationToken = default)
        {
            Verify.Operation(this.Compile != null, $"{nameof(Compile)} must be set first.");
            Verify.Operation(this.ReferencePath != null, $"{nameof(ReferencePath)} must be set first.");
            Verify.Operation(this.PluginPaths != null, $"{nameof(PluginPaths)} must be set first.");
            Verify.Operation(this.IntermediateOutputDirectory != null, $"{nameof(IntermediateOutputDirectory)} must be set first.");

            var compilation = this.CreateCompilation(cancellationToken);

            // For incremental build, we want to consider the input->output files as well as the assemblies involved in code generation.
            DateTime assembliesLastModified = GetLastModifiedAssemblyTime();

            var fileFailures = new List<Exception>();

            using (var hasher = System.Security.Cryptography.SHA1.Create())
            {
                foreach (var inputSyntaxTree in compilation.SyntaxTrees)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string sourceHash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(inputSyntaxTree.FilePath)), 0, 6).Replace('/', '-');
                    Logger.Info($"File \"{inputSyntaxTree.FilePath}\" hashed to {sourceHash}");
                    string outputFilePath = Path.Combine(this.IntermediateOutputDirectory, Path.GetFileNameWithoutExtension(inputSyntaxTree.FilePath) + $".{sourceHash}.generated.cs");

                    // Code generation is relatively fast, but it's not free.
                    // So skip files that haven't changed since we last generated them.
                    DateTime outputLastModified = File.Exists(outputFilePath) ? File.GetLastWriteTime(outputFilePath) : DateTime.MinValue;
                    if (File.GetLastWriteTime(inputSyntaxTree.FilePath) > outputLastModified || assembliesLastModified > outputLastModified)
                    {
                        int retriesLeft = 3;
                        do
                        {
                            try
                            {
                                var generatedSyntaxTree = await DocumentTransform.TransformAsync(
                                    compilation,
                                    inputSyntaxTree,
                                    this.ProjectDirectory,
                                    this.LoadPlugin,
                                    progress,
                                    cancellationToken);
                                var outputText = await generatedSyntaxTree.GetTextAsync(cancellationToken);
                                using (var outputFileStream = File.OpenWrite(outputFilePath))
                                using (var outputWriter = new StreamWriter(outputFileStream))
                                {
                                    outputText.Write(outputWriter, cancellationToken);

                                    // Truncate any data that may be beyond this point if the file existed previously.
                                    outputWriter.Flush();
                                    outputFileStream.SetLength(outputFileStream.Position);
                                }

                                if (!(generatedSyntaxTree is null))
                                {
                                    var root = await generatedSyntaxTree.GetRootAsync(cancellationToken);
                                    bool anyTypesGenerated = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Any();
                                    if (!anyTypesGenerated)
                                    {
                                        this.emptyGeneratedFiles.Add(outputFilePath);
                                    }
                                }
                                break;
                            }
                            catch (IOException ex) when (ex.HResult == ProcessCannotAccessFileHR && retriesLeft > 0)
                            {
                                retriesLeft--;
                                await Task.Delay(200, cancellationToken);
                            }
                            catch (Exception ex) when (!(ex is OperationCanceledException))
                            {
                                ReportError(progress, "CGR001", inputSyntaxTree, ex);
                                fileFailures.Add(ex);
                                break;
                            }
                        }
                        while (true);
                    }

                    this.generatedFiles.Add(outputFilePath);
                }
            }

            if (fileFailures.Count > 0)
            {
                throw new AggregateException(fileFailures);
            }
        }

        private Assembly LoadPlugin(AssemblyName assemblyName)
        {
            if (cachedPlugins.TryGetValue(assemblyName.Name, out var cached))
            {
                Logger.Info($"CGR retrieved cached plugin for {assemblyName.Name}: {cached.assembly.Location}");
                return cached.assembly;
            }
            Logger.Info($"CGR looking up plugin {assemblyName.Name}");
            var pluginPath = PluginPaths.FirstOrDefault(IsRequestedPlugin);
            if (pluginPath is null)
            {
                Logger.Info($"CGR didn't find plugin for {assemblyName.Name}");
                return null;
            }
            Logger.Info($"CGR loading up plugin {assemblyName.Name} from {pluginPath}");
            var loader = PluginLoader.CreateFromAssemblyFile(pluginPath, pluginSharedTypes);
            var assembly = loader.LoadDefaultAssembly();
            cachedPlugins[assemblyName.Name] = (loader, assembly);
            this.loadedAssemblies.Add(pluginPath);
            Logger.Info($"CGR loaded plugin for {assemblyName.Name}: {assembly.Location}");
            return assembly;

            bool IsRequestedPlugin(string path)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return string.Equals(assemblyName.Name, fileName, StringComparison.OrdinalIgnoreCase);
            }
        }

        private DateTime GetLastModifiedAssemblyTime()
        {
            var timestamps =
                from path in PluginPaths
                where File.Exists(path)
                select File.GetLastWriteTime(path);
            return timestamps.DefaultIfEmpty().Max();
        }

        private static void ReportError(IProgress<Diagnostic> progress, string id, SyntaxTree inputSyntaxTree, Exception ex)
        {
            Console.Error.WriteLine($"Exception in file processing: {ex}");

            if (progress == null)
            {
                return;
            }

            const string category = "CodeGen.Roslyn: Transformation";
            const string messageFormat = "{0}";

            var descriptor = new DiagnosticDescriptor(
                id,
                "Error during transformation",
                messageFormat,
                category,
                DiagnosticSeverity.Error,
                true);

            var location = inputSyntaxTree != null ? Location.Create(inputSyntaxTree, TextSpan.FromBounds(0, 0)) : Location.None;

            var messageArgs = new object[]
            {
                ex,
            };

            var reportDiagnostic = Diagnostic.Create(descriptor, location, messageArgs);

            progress.Report(reportDiagnostic);
        }

        private CSharpCompilation CreateCompilation(CancellationToken cancellationToken)
        {
            var compilation = CSharpCompilation.Create("codegen")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(this.ReferencePath.Select(p => MetadataReference.CreateFromFile(p)));
            var parseOptions = new CSharpParseOptions(preprocessorSymbols: this.PreprocessorSymbols);

            foreach (var sourceFile in this.Compile)
            {
                using (var stream = File.OpenRead(sourceFile))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = SourceText.From(stream);
                    compilation = compilation.AddSyntaxTrees(
                        CSharpSyntaxTree.ParseText(
                            text,
                            parseOptions,
                            sourceFile,
                            cancellationToken));
                }
            }

            return compilation;
        }
    }
}
