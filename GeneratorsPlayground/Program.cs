using System;
using System.Collections.Generic;

namespace GeneratorsPlayground
{
    internal class Program
    {
        static void Main(string[] args)
        {
            GeneratedNamespace.GeneratedClass.GeneratedMethod();

            Console.WriteLine(new R1(3.14f, DateTime.Now, new List<string> { "a", "b", "c" }));
        }
    }

    [Auto.BetterToString]
    partial record R1(float Num, DateTime Date, List<string> List)
    {
    }
}
