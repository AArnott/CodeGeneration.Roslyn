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
    using System.Text;
    using System.Threading;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using Microsoft.CodeAnalysis.Text;
    using Task = System.Threading.Tasks.Task;

    public class GenerateCodeFromAttributes : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

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

        public override bool Execute()
        {
            // Run under our own AppDomain so we can control the version of Roslyn we load.
            var appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationBase = Path.GetDirectoryName(this.GetType().Assembly.Location);
            appDomainSetup.DisallowBindingRedirects = true; // We want the version of Roslyn we compiled against.
            var appDomain = AppDomain.CreateDomain("codegen", AppDomain.CurrentDomain.Evidence, appDomainSetup);
            try
            {
                var helper = (Helper)appDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(Helper).FullName);
                helper.Compile = this.Compile;
                helper.TargetName = this.TargetName;
                helper.ReferencePath = this.ReferencePath;
                helper.GeneratorAssemblySearchPaths = this.GeneratorAssemblySearchPaths;
                helper.IntermediateOutputDirectory = this.IntermediateOutputDirectory;
                helper.Log = this.Log;

                try
                {
                    helper.Execute();

                    // Copy the contents of the output parameters into our own. Don't just copy the reference
                    // because we're going to unload the AppDomain.
                    this.GeneratedCompile = helper.GeneratedCompile.Select(i => new TaskItem(i)).ToArray();
                    this.AdditionalWrittenFiles = helper.AdditionalWrittenFiles.Select(i => new TaskItem(i)).ToArray();

                    return !this.Log.HasLoggedErrors;
                }
                catch (OperationCanceledException)
                {
                    this.Log.LogMessage(MessageImportance.High, "Canceled.");
                    return false;
                }
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }

        public void Cancel()
        {
            this.cts.Cancel();
        }

        private class Helper : MarshalByRefObject
        {
            public CancellationToken CancellationToken { get; set; }

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
                Task.Run(async delegate
                {
                    AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
                    {
                        return this.TryLoadAssembly(new AssemblyName(e.Name));
                    };

                    var project = this.CreateProject();
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

                    foreach (var inputDocument in project.Documents)
                    {
                        this.CancellationToken.ThrowIfCancellationRequested();

                        // Skip over documents that aren't on the prescribed list of files to scan.
                        if (!explicitIncludeList.Contains(inputDocument.Name))
                        {
                            continue;
                        }

                        string outputFilePath = Path.Combine(this.IntermediateOutputDirectory, Path.GetFileNameWithoutExtension(inputDocument.Name) + ".generated.cs");

                        // Code generation is relatively fast, but it's not free.
                        // And when we run the Simplifier.ReduceAsync it's dog slow.
                        // So skip files that haven't changed since we last generated them.
                        bool generated = false;
                        DateTime outputLastModified = File.GetLastWriteTime(outputFilePath);
                        if (File.GetLastWriteTime(inputDocument.Name) > outputLastModified || assembliesLastModified > outputLastModified)
                        {
                            this.Log.LogMessage(MessageImportance.Normal, "{0} -> {1}", inputDocument.Name, outputFilePath);

                            var outputDocument = await DocumentTransform.TransformAsync(
                                inputDocument,
                                new ProgressLogger(this.Log, inputDocument.Name));

                            // Only produce a new file if the generated document is not empty.
                            var semanticModel = await outputDocument.GetSemanticModelAsync(this.CancellationToken);
                            if (!CSharpDeclarationComputer.GetDeclarationsInSpan(semanticModel, TextSpan.FromBounds(0, semanticModel.SyntaxTree.Length), false, this.CancellationToken).IsEmpty)
                            {
                                var outputText = await outputDocument.GetTextAsync(this.CancellationToken);
                                using (var outputFileStream = File.OpenWrite(outputFilePath))
                                using (var outputWriter = new StreamWriter(outputFileStream))
                                {
                                    outputText.Write(outputWriter);

                                    // Truncate any data that may be beyond this point if the file existed previously.
                                    outputWriter.Flush();
                                    outputFileStream.SetLength(outputFileStream.Position);
                                }

                                generated = true;
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

                    SaveGeneratorAssemblyList(generatorAssemblyInputsFile);
                    writtenFiles.Add(new TaskItem(generatorAssemblyInputsFile));

                    this.GeneratedCompile = outputFiles.ToArray();
                    this.AdditionalWrittenFiles = writtenFiles.ToArray();
                }).GetAwaiter().GetResult();
            }

            private static DateTime GetLastModifiedAssemblyTime(string assemblyListPath)
            {
                if (!File.Exists(assemblyListPath))
                {
                    return DateTime.MinValue;
                }

                return (from path in File.ReadAllLines(assemblyListPath)
                        where File.Exists(path)
                        select File.GetLastWriteTime(path)).Max();
            }

            private static void SaveGeneratorAssemblyList(string assemblyListPath)
            {
                // Union our current list with the one on disk, since our incremental code generation
                // may have skipped some up-to-date files, resulting in fewer assemblies being loaded
                // this time.
                var assemblyPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(assemblyListPath))
                {
                    assemblyPaths.UnionWith(File.ReadAllLines(assemblyListPath));
                }

                assemblyPaths.UnionWith(
                    from a in AppDomain.CurrentDomain.GetAssemblies()
                    where !a.IsDynamic
                    select a.Location);

                File.WriteAllLines(
                    assemblyListPath,
                    assemblyPaths);
            }

            private Assembly TryLoadAssembly(AssemblyName assemblyName)
            {
                var referencePath = this.ReferencePath.FirstOrDefault(rp => string.Equals(rp.GetMetadata("FileName"), assemblyName.Name, StringComparison.OrdinalIgnoreCase));
                if (referencePath != null)
                {
                    return Assembly.LoadFile(referencePath.GetMetadata("FullPath"));
                }

                foreach (var searchPath in this.GeneratorAssemblySearchPaths)
                {
                    string searchDir = searchPath.GetMetadata("FullPath");
                    const string extension = ".dll";
                    string fileName = Path.Combine(searchDir, assemblyName.Name + extension);
                    if (File.Exists(fileName))
                    {
                        return Assembly.LoadFile(fileName);
                    }
                }

                return null;
            }

            private Project CreateProject()
            {
                var workspace = new AdhocWorkspace();
                var project = workspace.CurrentSolution.AddProject("codegen", "codegen", "C#")
                    .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .WithMetadataReferences(this.ReferencePath.Select(p => MetadataReference.CreateFromFile(p.ItemSpec)));

                foreach (var sourceFile in this.Compile)
                {
                    using (var stream = File.OpenRead(sourceFile.ItemSpec))
                    {
                        this.CancellationToken.ThrowIfCancellationRequested();
                        var text = SourceText.From(stream);
                        project = project.AddDocument(sourceFile.ItemSpec, text).Project;
                    }
                }

                return project;
            }
        }

        private class ProgressLogger : IProgress<Diagnostic>
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
                            lineSpan.StartLinePosition.Line,
                            lineSpan.StartLinePosition.Character,
                            lineSpan.EndLinePosition.Line,
                            lineSpan.EndLinePosition.Character,
                            MessageImportance.Normal,
                            value.GetMessage(CultureInfo.CurrentCulture));
                        break;
                    case DiagnosticSeverity.Warning:
                        this.logger.LogWarning(
                            value.Descriptor.Category,
                            value.Descriptor.Id,
                            value.Descriptor.HelpLinkUri,
                            value.Location.SourceTree.FilePath,
                            lineSpan.StartLinePosition.Line,
                            lineSpan.StartLinePosition.Character,
                            lineSpan.EndLinePosition.Line,
                            lineSpan.EndLinePosition.Character,
                            value.GetMessage(CultureInfo.CurrentCulture));
                        break;
                    case DiagnosticSeverity.Error:
                        this.logger.LogError(
                            value.Descriptor.Category,
                            value.Descriptor.Id,
                            value.Descriptor.HelpLinkUri,
                            value.Location.SourceTree.FilePath,
                            lineSpan.StartLinePosition.Line,
                            lineSpan.StartLinePosition.Character,
                            lineSpan.EndLinePosition.Line,
                            lineSpan.EndLinePosition.Character,
                            value.GetMessage(CultureInfo.CurrentCulture));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
