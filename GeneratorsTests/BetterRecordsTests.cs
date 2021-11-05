using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

namespace GeneratorsTests
{
    [TestFixture]
    public class BetterRecordsTests
    {
        [Test]
        public void SimpleGeneratorTest()
        {
            string source = @"using System;
using System.Collections.Generic;

namespace GeneratedDemo
{
    [Auto.BetterToString]
    partial record R1(float Num, DateTime Date, List<string> List)
    {        
    }
}";
            Compilation comp = CreateCompilation(source);
            Compilation newComp = RunGenerators(comp, out ImmutableArray<Diagnostic> diagnostics, new BetterRecordToStringGenerator());
            Assert.That(diagnostics, Is.Empty);
            IEnumerable<SyntaxTree> generatedTrees = newComp.RemoveSyntaxTrees(comp.SyntaxTrees).SyntaxTrees;

            var root = generatedTrees.Last().GetRoot() as CompilationUnitSyntax;

            var actual = root.DescendantNodes().OfType<RecordDeclarationSyntax>().Single().ToString();

            Assert.AreEqual(@"partial record R1
    {

         protected virtual bool PrintMembers(System.Text.StringBuilder builder) 
         {         
               builder.Append(""Num"");
               builder.Append("" = "");
               builder.Append(Auto.RecordHelper.FormatValue(Num));
               builder.Append("", "");
               builder.Append(""Date"");
               builder.Append("" = "");
               builder.Append(Auto.RecordHelper.FormatValue(Date));
               builder.Append("", "");
               builder.Append(""List"");
               builder.Append("" = "");
               builder.Append(Auto.RecordHelper.FormatValue(List));
               builder.Append("", "");

               return true; 
         }
    }", actual);

        }

        private static Compilation CreateCompilation(string source, OutputKind outputKind = OutputKind.ConsoleApplication)
            => CSharpCompilation.Create("compilation",
                new[] { CSharpSyntaxTree.ParseText(source, new(LanguageVersion.Preview)) },
                new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
                new(outputKind));

        private static GeneratorDriver CreateDriver(Compilation c, params ISourceGenerator[] generators)
            => CSharpGeneratorDriver.Create(generators, parseOptions: (CSharpParseOptions)c.SyntaxTrees.First().Options);

        private static Compilation RunGenerators(Compilation c, out ImmutableArray<Diagnostic> diagnostics, params ISourceGenerator[] generators)
        {
            CreateDriver(c, generators).RunGeneratorsAndUpdateCompilation(c, out var d, out diagnostics);
            return d;
        }
    }
}

