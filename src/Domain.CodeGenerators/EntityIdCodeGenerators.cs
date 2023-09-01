using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Domain.CodeGenerators;

[Generator]
public class EntityIdCodeGenerators : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var compilation = context.Compilation;
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            if (syntaxTree.TryGetText(out var sourceText) &&
                !sourceText.ToString().Contains("IInt64StronglyTypedId"))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            if (semanticModel == null)
            {
                continue;
            }

            var typeDeclarationSyntaxs = syntaxTree.GetRoot().DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>();
            foreach (var tds in typeDeclarationSyntaxs)
            {
                Generate(context, semanticModel, tds);
            }
        }
    }

    private void Generate(GeneratorExecutionContext context, SemanticModel semanticModel,
        TypeDeclarationSyntax classDef)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDef);
        if (!(symbol is INamedTypeSymbol)) return;
        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)symbol;
        var isEntityId = namedTypeSymbol.Interfaces
            .SingleOrDefault(t => t.Name.StartsWith("IInt64StronglyTypedId"));
        if (isEntityId == null) return;
        string ns = namedTypeSymbol.ContainingNamespace.ToString();
        string className = namedTypeSymbol.Name;

        // var constructor = namedTypeSymbol.Constructors.FirstOrDefault(t => t.Parameters.Count() == 1);
        //
        // if (constructor == null)
        // {
        //     return;
        // }

        string source = $@"// <auto-generated/>
using NetCorePal.Extensions.Domain;
using System.ComponentModel;
namespace {ns}
{{
    [TypeConverter(typeof(EntityIdTypeConverter<{className}, long>))]
    public partial record {className}(long Id) : IInt64StronglyTypedId
    {{
        public static implicit operator long({className} id) => id.Id;
        public static implicit operator {className}(long id) => new {className}(id);
        public override string ToString()
        {{
            return Id.ToString();
        }}
    }}
}}
";
        context.AddSource($"{className}.g.cs", source);
    }
}