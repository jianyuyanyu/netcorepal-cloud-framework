using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetCorePal.Extensions.Domain.SourceGenerators;

[Generator]
public class EntityIdCodeGenerators : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntaxProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is TypeDeclarationSyntax,
                transform: (syntaxContext, _) => (TypeDeclarationSyntax)syntaxContext.Node)
            .Where(tds => tds != null);

        var compilationAndTypes = context.CompilationProvider.Combine(syntaxProvider.Collect());

        context.RegisterSourceOutput(compilationAndTypes, (spc, source) =>
        {
            var (compilation, typeDeclarations) = source;
            foreach (var tds in typeDeclarations)
            {
                var semanticModel = compilation.GetSemanticModel(tds.SyntaxTree);
                Generate(spc, semanticModel, tds, SourceType.Int64);
                Generate(spc, semanticModel, tds, SourceType.Int32);
                Generate(spc, semanticModel, tds, SourceType.String);
                Generate(spc, semanticModel, tds, SourceType.Guid);
            }
        });
    }

    private void Generate(SourceProductionContext context, SemanticModel semanticModel,
        TypeDeclarationSyntax classDef, SourceType sourceType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDef);
        if (symbol is not INamedTypeSymbol namedTypeSymbol) return;

        var isEntityId = namedTypeSymbol.Interfaces
            .SingleOrDefault(t => t.Name.StartsWith($"I{sourceType}StronglyTypedId"));
        if (isEntityId == null) return;

        string ns = namedTypeSymbol.ContainingNamespace.ToString();
        string className = namedTypeSymbol.Name;

        string source = $@"// <auto-generated/>
using NetCorePal.Extensions.Domain;
using System;
using System.ComponentModel;
namespace {ns}
{{
    /// <summary>
    /// Strongly typed id for {className}
    /// </summary>
    /// <param name=""Id"">The Inner Id</param>
    [TypeConverter(typeof(EntityIdTypeConverter<{className}, {sourceType}>))]
    public partial record {className}({sourceType} Id) : I{sourceType}StronglyTypedId
    {{
        ///// <summary>
        ///// implicit operator
        ///// </summary>
        //public static implicit operator {sourceType}({className} id) => id.Id;
        ///// <summary>
        ///// implicit operator
        ///// </summary>
        //public static implicit operator {className}({sourceType} id) => new {className}(id);

        /// <summary>
        /// Id.ToString()
        /// </summary>
        public override string ToString()
        {{
            return Id.ToString();
        }}
#nullable enable
        /// <summary>
        /// Equals
        /// </summary>
        public virtual bool Equals({className}? other)
#nullable disable
        {{
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }}
        /// <summary>
        /// Id.GetHashCode()
        /// </summary>
        public override int GetHashCode() => Id.GetHashCode();
    }}
}}
";
        context.AddSource($"{ns}.{className}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    enum SourceType
    {
        String,
        Int64,
        Int32,
        Guid
    }
}