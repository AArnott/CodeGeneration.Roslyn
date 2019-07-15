using CodeGeneration.Roslyn;
using System;

namespace Sample.Generator
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    [CodeGenerationAttribute(typeof(IdGenerator))]
    public class GeneratedIdAttribute : Attribute
    {
    }
}
