using Sample.Generator;
using System;

namespace Sample.Consumer
{
    [GeneratedId]
    public partial class Class1
    {
        public void TestId()
        {
            if (this.Id != Guid.Empty)
            {
                Console.WriteLine("Generated Id!");
            }
        }
    }
}
