using System;
using CodeGeneration.Roslyn;

namespace JsonConsumer
{
    [CodeGenerationAttribute("JsonGenerator.JsonGenerator, JsonGenerator")]
    class JsonAttribute : Attribute { }

    [Json]
    partial class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Json of generator assembly full name:");
            Console.WriteLine(Json);
        }
    }
}
