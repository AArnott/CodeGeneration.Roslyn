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
    using Task = System.Threading.Tasks.Task;

    public class GenerateCodeFromAttributes : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

#if NETCOREAPP1_0
        private TaskLoadContext taskLoadContext;
#endif

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

        internal CancellationToken CancellationToken => this.cts.Token;

        public override bool Execute()
        {
#if NET46
            // Run under our own AppDomain so we can control the version of Roslyn we load.
            var appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationBase = Path.GetDirectoryName(this.GetType().Assembly.Location);
            appDomainSetup.ConfigurationFile = Assembly.GetExecutingAssembly().Location + ".config";
            var appDomain = AppDomain.CreateDomain("codegen", AppDomain.CurrentDomain.Evidence, appDomainSetup);
            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
#else

#endif
            try
            {
                var helperAssemblyName = new AssemblyName(typeof(GenerateCodeFromAttributes).GetTypeInfo().Assembly.GetName().FullName.Replace(ThisAssembly.AssemblyName, ThisAssembly.AssemblyName + ".Helper"));
                const string helperTypeName = "CodeGeneration.Roslyn.Tasks.Helper";
#if NET46
                var helper = (Helper)appDomain.CreateInstanceAndUnwrap(helperAssemblyName.FullName, helperTypeName);
#else
                this.taskLoadContext = new TaskLoadContext(Path.GetDirectoryName(new Uri(typeof(GenerateCodeFromAttributes).GetTypeInfo().Assembly.CodeBase).LocalPath));
                var helperAssembly = this.taskLoadContext.LoadFromAssemblyName(helperAssemblyName);
                var helperTypeInContext = helperAssembly.GetType(helperTypeName);
                dynamic helper = Activator.CreateInstance(helperTypeInContext, this.taskLoadContext);
#endif
                helper.Compile = this.Compile;
                helper.TargetName = this.TargetName;
                helper.ReferencePath = this.ReferencePath;
                helper.GeneratorAssemblySearchPaths = this.GeneratorAssemblySearchPaths;
                helper.IntermediateOutputDirectory = this.IntermediateOutputDirectory;
                helper.Log = this.Log;

                try
                {
                    this.CancellationToken.ThrowIfCancellationRequested();
                    using (this.CancellationToken.Register(() => helper.Cancel(), false))
                    {
                        helper.Execute();
                    }

                    // Copy the contents of the output parameters into our own. Don't just copy the reference
                    // because we're going to unload the AppDomain.
                    this.GeneratedCompile = ((IEnumerable<ITaskItem>)helper.GeneratedCompile).Select(i => new TaskItem(i)).ToArray();
                    this.AdditionalWrittenFiles = ((IEnumerable<ITaskItem>)helper.AdditionalWrittenFiles).Select(i => new TaskItem(i)).ToArray();

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
#if NET46
                AppDomain.CurrentDomain.AssemblyResolve -= this.CurrentDomain_AssemblyResolve;
                AppDomain.Unload(appDomain);
#endif
            }
        }

#if NET46
        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                return Assembly.Load(args.Name);
            }
            catch
            {
                return null;
            }
        }
#endif

        public void Cancel() => this.cts.Cancel();

#if NETCOREAPP1_0
        private class TaskLoadContext : AssemblyLoadContext
        {
            private readonly string assemblyLoadPath;

            internal TaskLoadContext(string assemblyLoadPath)
            {
                this.assemblyLoadPath = assemblyLoadPath;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (assemblyName.Name.StartsWith("Microsoft.Build", StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                {
                    // MSBuild and System.* make up our exchange types. So don't load them in this LoadContext.
                    // We need to inherit them from the default load context.
                    return null;
                }

                string assemblyPath = Path.Combine(this.assemblyLoadPath, assemblyName.Name) + ".dll";
                if (File.Exists(assemblyPath))
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }
        }
#endif
    }
}
