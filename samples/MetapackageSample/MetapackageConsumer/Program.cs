using System;
using MetapackageSample;

namespace MetapackageConsumer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(TestGenerated.Text());
        }

    }

    [DuplicateWithSuffix("Generated")]
    class Test {
        public static string Text() => "Success!";
    }
}
