using System;

namespace CodeGeneration.Roslyn.Tests.Generators
{
    public class TestAttribute : Attribute
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
