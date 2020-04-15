// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using System.Threading;

namespace CodeGeneration.Roslyn.Generate
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using CodeGeneration.Roslyn.Engine;
    using CodeGeneration.Roslyn.Tool.CommandLine;
    using Microsoft.CodeAnalysis;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var cancellationToken = cancellationTokenSource.Token;
                Console.CancelKeyPress += ConsoleOnCancelKeyPress;

                void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
                {
                    Console.CancelKeyPress -= ConsoleOnCancelKeyPress;
                    cancellationTokenSource.Cancel();
                    e.Cancel = true;
                }

                return await Core(args, cancellationToken);
            }
        }

        private static async Task<int> Core(string[] args, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> compile = Array.Empty<string>();
            IReadOnlyList<string> refs = Array.Empty<string>();
            IReadOnlyList<string> preprocessorSymbols = Array.Empty<string>();
            IReadOnlyList<string> plugins = Array.Empty<string>();
            IReadOnlyList<string> buildPropertiesList = Array.Empty<string>();
            string generatedCompileItemFile = null;
            string outputDirectory = null;
            string projectDir = null;
            bool version = false;
            Dictionary<string, string> buildProperties = new Dictionary<string, string>();
            
            ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("version", ref version, "Show version of this tool (and exit).");
                syntax.DefineOptionList("r|reference", ref refs, "Paths to assemblies being referenced");
                syntax.DefineOptionList("d|define", ref preprocessorSymbols, "Preprocessor symbols");
                syntax.DefineOptionList("plugin", ref plugins, "Paths to generator plugin assemblies");
                syntax.DefineOptionList("buildProperty", ref buildPropertiesList, false, "MSBuild properties to expose to generators");
                syntax.DefineOption("out", ref outputDirectory, true, "The directory to write generated source files to");
                syntax.DefineOption("projectDir", ref projectDir, true, "The absolute path of the directory where the project file is located");
                syntax.DefineOption("generatedFilesList", ref generatedCompileItemFile, "The path to the file to create with a list of generated source files");
                syntax.DefineParameterList("compile", ref compile, "Source files included in compilation");
            });

            if (version)
            {
                Console.WriteLine(ThisAssembly.AssemblyInformationalVersion);
                return 0;
            }
            if (!compile.Any())
            {
                Console.Error.WriteLine("No source files are specified.");
                return 1;
            }

            if (outputDirectory == null)
            {
                Console.Error.WriteLine("The output directory must be specified.");
                return 2;
            }

            foreach (var prop in buildPropertiesList) 
            {
                var i = prop.IndexOf("=");

                if (i <= 0) 
                {
                    continue;
                }

                var key = prop.Substring(0, i);
                var value = prop.Substring(i + 1);
                buildProperties[key] = value;
            }

            var generator = new CompilationGenerator
            {
                ProjectDirectory = projectDir,
                Compile = Sanitize(compile),
                ReferencePath = Sanitize(refs),
                PreprocessorSymbols = preprocessorSymbols,
                PluginPaths = Sanitize(plugins),
                BuildProperties = Sanitize(buildProperties),
                IntermediateOutputDirectory = outputDirectory,
            };

            var progress = new Progress<Diagnostic>(OnDiagnosticProgress);

            try
            {
                await generator.GenerateAsync(progress, cancellationToken);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{e.GetType().Name}: {e.Message}");
                Console.Error.WriteLine(e.ToString());
                return 3;
            }

            if (generatedCompileItemFile != null)
            {
                await File.WriteAllLinesAsync(generatedCompileItemFile, generator.GeneratedFiles, cancellationToken);
            }

            foreach (var file in generator.GeneratedFiles)
            {
                Logger.Info(file);
            }

            return 0;
        }

        private static void OnDiagnosticProgress(Diagnostic diagnostic)
        {
            Console.WriteLine(diagnostic.ToString());
        }

        private static IReadOnlyList<string> Sanitize(IReadOnlyList<string> inputs)
        {
            return inputs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        }

        private static IReadOnlyDictionary<string, string> Sanitize(IReadOnlyDictionary<string, string> inputs)
        {
            return inputs.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value.Trim());
        }
    }
}