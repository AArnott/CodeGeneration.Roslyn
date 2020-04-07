using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;

namespace SourceGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute("SourceGenerator.Attributes.Generators.HelloWorldGenerator, SourceGenerator.Attributes.Generators")]
    [Conditional("CodeGeneration")]
    public class HelloWorldAttribute : Attribute
    {
    }
}
