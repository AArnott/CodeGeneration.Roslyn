using System;
using CodeGeneration.Roslyn;

namespace BuildPropsConsumer
{

    [CodeGenerationAttribute("BuildPropsGenerator.FrameworkInfoProviderGenerator, BuildPropsGenerator")]
    class FrameworkInfoProviderAttribute : Attribute { }

    [FrameworkInfoProvider]
    partial class Program
    {
        static void Main(string[] args)
        {
            var program = new Program();
            var frameworks = program.TargetFrameworks;
            var currentFramework = program.CurrentTargetFramework;

            Console.WriteLine("This project is build for the following frameworks: ");

            foreach(var framework in frameworks) {
                var message = framework == currentFramework ? $"{framework} (current)" : framework;
                Console.WriteLine(message);
            }
        }
    }
}
