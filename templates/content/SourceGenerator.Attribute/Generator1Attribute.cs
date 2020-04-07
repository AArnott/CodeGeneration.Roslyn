using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;

namespace SourceGenerator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute("SourceGenerator.Generators.Generator1, SourceGenerator.Generators")]
    [Conditional("CodeGeneration")]
    public class Generator1Attribute : Attribute
    {
    }
}
