// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tasks
{
    using System.IO;
    using System.Linq;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class GenerateCodeFromAttributes : ToolTask
    {
        private string generatedCompileItemsFilePath;

        [Required]
        public ITaskItem[] Compile { get; set; }

        [Required]
        public ITaskItem[] ReferencePath { get; set; }

        [Required]
        public ITaskItem[] GeneratorAssemblySearchPaths { get; set; }

        [Required]
        public string IntermediateOutputDirectory { get; set; }

        public string ToolLocationOverride { get; set; }

        [Output]
        public ITaskItem[] GeneratedCompile { get; set; }

        [Output]
        public ITaskItem[] AdditionalWrittenFiles { get; set; }

        protected override string ToolName => "dotnet";

        protected override string GenerateFullPathToTool() => this.ToolName;

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            this.UseCommandProcessor = true;

            int exitCode = base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);

            if (exitCode == 0)
            {
                this.AdditionalWrittenFiles = new ITaskItem[] { new TaskItem(this.generatedCompileItemsFilePath) };
                this.GeneratedCompile = File.ReadAllLines(this.generatedCompileItemsFilePath).Select(f => new TaskItem(f)).ToArray();
            }

            return exitCode;
        }

        protected override string GenerateCommandLineCommands()
        {
            var argBuilder = new CommandLineBuilder();

            argBuilder.AppendFileNameIfNotNull(string.IsNullOrWhiteSpace(this.ToolLocationOverride)
                ? "codegen"
                : this.ToolLocationOverride);

            foreach (var item in this.ReferencePath)
            {
                argBuilder.AppendSwitch("-r");
                argBuilder.AppendFileNameIfNotNull(item);
            }

            foreach (var item in this.GeneratorAssemblySearchPaths)
            {
                argBuilder.AppendSwitch("--generatorSearchPath");
                argBuilder.AppendFileNameIfNotNull(item);
            }

            argBuilder.AppendSwitch("--out");
            argBuilder.AppendFileNameIfNotNull(this.IntermediateOutputDirectory);

            this.generatedCompileItemsFilePath = Path.Combine(this.IntermediateOutputDirectory, Path.GetRandomFileName());
            argBuilder.AppendSwitch("--generatedFilesList");
            argBuilder.AppendFileNameIfNotNull(this.generatedCompileItemsFilePath);

            argBuilder.AppendSwitch("--");
            argBuilder.AppendFileNamesIfNotNull(this.Compile, " ");

            return argBuilder.ToString();
        }
    }
}
