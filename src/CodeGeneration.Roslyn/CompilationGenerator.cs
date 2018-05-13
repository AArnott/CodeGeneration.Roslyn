// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.Extensions.DependencyModel;
    using Microsoft.Extensions.DependencyModel.Resolution;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Validation;

    public class CompilationGenerator
    {
        private const string InputAssembliesIntermediateOutputFileName = "CodeGeneration.Roslyn.InputAssemblies.txt";
        private const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);
        private static readonly HashSet<string> AllowedAssemblyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll" };

        private readonly List<string> emptyGeneratedFiles = new List<string>();
        private readonly List<string> generatedFiles = new List<string>();
        private readonly List<string> additionalWrittenFiles = new List<string>();
        private readonly List<string> loadedAssemblies = new List<string>();
        private readonly Dictionary<string, Assembly> assembliesByPath = new Dictionary<string, Assembly>();
        private readonly HashSet<string> directoriesWithResolver = new HashSet<string>();
        private CompositeCompilationAssemblyResolver assemblyResolver;
        private DependencyContext dependencyContext;

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

        public string ProjectDirectory { get; set; }

        public CompilationGenerator()
        {
            this.assemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
            {
                new ReferenceAssemblyPathResolver(),
                new PackageCompilationAssemblyResolver()
            });
            this.dependencyContext = DependencyContext.Default;

            var loadContext = AssemblyLoadContext.GetLoadContext(this.GetType().GetTypeInfo().Assembly);
            loadContext.Resolving += this.ResolveAssembly;
        }

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

            var fileFailures = new List<Exception>();

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
                        int retriesLeft = 3;
                        do
                        {
                            try
                            {
                                var generatedSyntaxTree = DocumentTransform.TransformAsync(
                                    compilation,
                                    inputSyntaxTree,
                                    this.ProjectDirectory,
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

                                break;
                            }
                            catch (IOException ex) when (ex.HResult == ProcessCannotAccessFileHR && retriesLeft > 0)
                            {
                                retriesLeft--;
                                Task.Delay(200).Wait();
                            }
                            catch (Exception ex)
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

            this.SaveGeneratorAssemblyList(generatorAssemblyInputsFile);

            if (fileFailures.Count > 0)
            {
                throw new AggregateException(fileFailures);
            }
        }

        protected virtual Assembly LoadAssembly(string path)
        {
            if (this.assembliesByPath.ContainsKey(path))
                return this.assembliesByPath[path];

            var loadContext = AssemblyLoadContext.GetLoadContext(this.GetType().GetTypeInfo().Assembly);
            var assembly = loadContext.LoadFromAssemblyPath(path);

            var newDependencyContext = DependencyContext.Load(assembly);
            if(newDependencyContext!=null)
                this.dependencyContext = this.dependencyContext.Merge(newDependencyContext);
            var basePath = Path.GetDirectoryName(path);
            if (!this.directoriesWithResolver.Contains(basePath))
            {
                this.assemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
                {
                    new AppBaseCompilationAssemblyResolver(basePath),
                    this.assemblyResolver
                });
                this.directoriesWithResolver.Add(basePath);
            }

            this.assembliesByPath.Add(path, assembly);
            return assembly;
        }

        private Assembly ResolveAssembly(AssemblyLoadContext context, AssemblyName name)
        {
            var library = this.dependencyContext.RuntimeLibraries.FirstOrDefault(runtime => string.Equals(runtime.Name, name.Name, StringComparison.OrdinalIgnoreCase));
            if (library == null)
                return null;
            var wrapper = new CompilationLibrary(
                library.Type,
                library.Name,
                library.Version,
                library.Hash,
                library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
                library.Dependencies,
                library.Serviceable);

            var assemblyPathes = new List<string>();
            this.assemblyResolver.TryResolveAssemblyPaths(wrapper, assemblyPathes);

            return assemblyPathes.Select(context.LoadFromAssemblyPath).FirstOrDefault();
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
                ex
            };

            var reportDiagnostic = Diagnostic.Create(descriptor, location, messageArgs);

            progress.Report(reportDiagnostic);
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
