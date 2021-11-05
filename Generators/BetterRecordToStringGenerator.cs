using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators
{
    [Generator]
    public class BetterRecordToStringGenerator : ISourceGenerator
    {
        private const string HELPER_TEXT = @"using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Auto
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class BetterToStringAttribute : Attribute
    {
        public BetterToStringAttribute() { }
    }

    static class RecordHelper
    {
        public static bool PrintMembers(object value, StringBuilder builder)
        {
            var text = FormatObject(value);
            if (string.IsNullOrEmpty(text))
                return false;
            else
            {
                builder.Append(text);
                return true;
            }
        }

        public static string FormatObject(object value) =>
            value is null
                ? null
                : string.Join(""; "",
                    value.GetType().GetProperties().Select(p => p.Name + "" = "" + FormatValue(p.GetValue(value)))
                );

        public static string FormatValue(object value) =>
            value switch
            {
                null => ""∅"",
                bool b => b ? ""true"" : ""false"",
                string s => $""\""{s}\"""",
                char c => $""\'{c}\'"",
                DateTime dt => dt.ToString(""o"", CultureInfo.InvariantCulture),
                IFormattable @if => @if.ToString(null, CultureInfo.InvariantCulture),
                IEnumerable ie => ""["" + string.Join("", "", ie.Cast<object>().Select(o => FormatValue(o))) + ""]"",
                _ => value.ToString()
            };
    }
}
";

        public void Initialize(GeneratorInitializationContext context) => context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("RecordHelper", SourceText.From(HELPER_TEXT, Encoding.UTF8));

            if (context.SyntaxReceiver is not SyntaxReceiver receiver) return;

            var options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(HELPER_TEXT, Encoding.UTF8), options));

            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("Auto.BetterToStringAttribute");
            INamedTypeSymbol recordHelperSymbol = compilation.GetTypeByMetadataName("Auto.RecordHelper");

            // loop over the candidate fields, and keep the ones that are actually annotated
            var recordSymbols = new List<(INamedTypeSymbol TypeSymbol, IEnumerable<string> Properties)>();
            foreach (var record in receiver.CandidateTypes)
            {
                var model = compilation.GetSemanticModel(record.SyntaxTree);
                var recordSymbol = model.GetDeclaredSymbol(record);

                if (recordSymbol.GetAttributes().Any(ad =>
                    ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                    recordSymbols.Add(
                        (recordSymbol,
                            record.ChildNodes().OfType<PropertyDeclarationSyntax>().Select(p => p.Identifier.Text)
                            .Concat(
                            record.ParameterList.ChildNodes().OfType<ParameterSyntax>().Select(p => p.Identifier.Text)
                            ).ToList()
                        ));
            }

            // group the fields by class, and generate the source


            foreach (var rs in recordSymbols)
            {
                string classSource = ProcessRecord(rs.TypeSymbol, rs.Properties, attributeSymbol, recordHelperSymbol, context);
                context.AddSource($"{rs.TypeSymbol.Name}_betterToString.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }

        private string ProcessRecord(INamedTypeSymbol classSymbol, IEnumerable<string> properties, INamedTypeSymbol attributeSymbol, INamedTypeSymbol recordHelperSymbol, in GeneratorExecutionContext context)
        {
            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default)) return null; //TODO: issue a diagnostic that it must be top level

            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            var source = new StringBuilder($@"
namespace {namespaceName}
{{
    partial record {classSymbol.Name}
    {{

         protected virtual bool PrintMembers(System.Text.StringBuilder builder) 
         {{         
");

            foreach (var property in properties)
                ProcessProperty(source, property, recordHelperSymbol);

            source.Append(@"
               return true; 
         }
    }
}");
            return source.ToString();
        }

        private void ProcessProperty(StringBuilder source, string property, INamedTypeSymbol recordHelperSymbol)
        {
            source.AppendLine($"               builder.Append(\"{property}\");");
            source.AppendLine($"               builder.Append(\" = \");");
            source.AppendLine($"               builder.Append({recordHelperSymbol.ToDisplayString()}.FormatValue({property}));");
            source.AppendLine($"               builder.Append(\", \");");
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            private readonly List<RecordDeclarationSyntax> _candidateTypes = new List<RecordDeclarationSyntax>();

            public IReadOnlyCollection<RecordDeclarationSyntax> CandidateTypes => _candidateTypes;

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is RecordDeclarationSyntax { AttributeLists: { Count: > 0 } } rds && rds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    _candidateTypes.Add(rds);
            }
        }
    }
}
