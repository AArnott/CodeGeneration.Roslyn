using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;

namespace MetapackageSample
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    [CodeGenerationAttribute("MetapackageSample.Generators.DuplicateWithSuffixGenerator, MetapackageSample.Generators")]
    [Conditional("CodeGeneration")]
    public class DuplicateWithSuffixAttribute : Attribute
    {
        public DuplicateWithSuffixAttribute(string suffix)
        {
            Suffix = suffix;
        }

        public string Suffix { get; }
    }
}
