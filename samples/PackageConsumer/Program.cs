using System;
using CodeGeneration.Roslyn;

namespace PackageConsumer
{
    [CodeGenerationAttribute("Sample.Generator.IdGenerator, PackagedGenerator")]
    class IdAttribute : Attribute { }

    [Id]
    partial class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Generated 'Id' property value: " + new Program().Id);
        }
    }
}
