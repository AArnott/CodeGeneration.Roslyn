// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Tasks
{
    using System.IO;
    using System.Linq;
    using System.Text;
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
        public string ProjectDirectory { get; set; }

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

            return argBuilder.ToString();
        }

        protected override string GenerateResponseFileCommands()
        {
            var argBuilder = new StringBuilder();

            foreach (var item in this.ReferencePath)
            {
                argBuilder.AppendLine("-r");
                argBuilder.AppendLine(item.ItemSpec);
            }

            foreach (var item in this.GeneratorAssemblySearchPaths)
            {
                argBuilder.AppendLine("--generatorSearchPath");
                argBuilder.AppendLine(item.ItemSpec);
            }

            argBuilder.AppendLine("--out");
            argBuilder.AppendLine(this.IntermediateOutputDirectory);

            argBuilder.AppendLine("--projectDir");
            argBuilder.AppendLine(this.ProjectDirectory);

            this.generatedCompileItemsFilePath = Path.Combine(this.IntermediateOutputDirectory, Path.GetRandomFileName());
            argBuilder.AppendLine("--generatedFilesList");
            argBuilder.AppendLine(this.generatedCompileItemsFilePath);

            argBuilder.AppendLine("--");
            foreach (var item in this.Compile)
            {
                argBuilder.AppendLine(item.ItemSpec);
            }

            return argBuilder.ToString();
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            MessageImportance newImportance;
            if (DidExtractPrefix("High"))
                newImportance = MessageImportance.High;
            else if (DidExtractPrefix("Normal"))
                newImportance = MessageImportance.Normal;
            else if (DidExtractPrefix("Low"))
                newImportance = MessageImportance.Low;
            else
                newImportance = messageImportance;

            if (newImportance < messageImportance)
                messageImportance = newImportance; // Lower value => higher importance

            base.LogEventsFromTextOutput(singleLine, messageImportance);

            bool DidExtractPrefix(string importanceString)
            {
                var prefix = $"::{importanceString}::";
                if (singleLine.StartsWith(prefix))
                {
                    singleLine = singleLine.Substring(prefix.Length);
                    return true;
                }
                return false;
            }
        }
    }
}
