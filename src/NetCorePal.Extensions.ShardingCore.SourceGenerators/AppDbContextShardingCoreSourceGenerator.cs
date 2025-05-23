﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetCorePal.Extensions.ShardingCore.SourceGenerators
{
    [Generator]
    public class AppDbContextShardingCoreSourceGenerator : IIncrementalGenerator
    {
        private readonly IReadOnlyCollection<string> dbContextBaseNames = new[]
            { "AppDbContextBase", "AppIdentityDbContextBase", "AppIdentityUserContextBase" };

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
                    var symbol = semanticModel.GetDeclaredSymbol(tds);
                    if (symbol is INamedTypeSymbol namedTypeSymbol)
                    {
                        if (namedTypeSymbol.IsAbstract || !dbContextBaseNames.Contains(namedTypeSymbol.BaseType?.Name))
                        {
                            continue;
                        }

                        List<INamedTypeSymbol> ids = GetAllStrongTypedId(compilation);
                        if (namedTypeSymbol.AllInterfaces.Any(i => i.Name == "IShardingCore"))
                        {
                            GenerateShardingCore(spc, namedTypeSymbol);
                        }
                    }
                }
            });
        }
        
        private void GenerateShardingCore(SourceProductionContext context, INamedTypeSymbol dbContextType)
        {
            var ns = dbContextType.ContainingNamespace.ToString();
            string className = dbContextType.Name;
            StringBuilder sb = new();

            string source = $@"// <auto-generated/>
using Microsoft.EntityFrameworkCore;
using NetCorePal.Extensions.Repository.EntityFrameworkCore;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.Extensions;
namespace {ns}
{{
    /// <summary>
    /// {className} ShardingCore
    /// </summary>
    public partial class {className} : IShardingDbContext, IShardingTableDbContext
    {{
        private bool _createExecutor = false;
        private IShardingDbContextExecutor _shardingDbContextExecutor;
        /// <summary>
        /// 分片执行者
        /// </summary>
        /// <returns></returns>
        public IShardingDbContextExecutor GetShardingExecutor()
        {{
            if (!_createExecutor)
            {{
                _shardingDbContextExecutor = this.CreateShardingDbContextExecutor();
                _createExecutor = true;
            }}
            return _shardingDbContextExecutor;
        }}

        /// <summary>
        /// 当前dbcontext是否是执行的dbcontext
        /// </summary>
        public bool IsExecutor => GetShardingExecutor() == default;
        
        public override async ValueTask DisposeAsync()
        {{
            if (_shardingDbContextExecutor != null)
            {{
                await _shardingDbContextExecutor.DisposeAsync();
            }}

            await base.DisposeAsync();
        }}

        public IRouteTail RouteTail {{ get; set; }}
    }}
}}
";
            context.AddSource($"{className}ShardingCore.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private List<INamedTypeSymbol> GetAllTypes(IAssemblySymbol assemblySymbol)
        {
            var types = new List<INamedTypeSymbol>();
            GetTypesInNamespace(assemblySymbol.GlobalNamespace, types);
            return types;
        }
        
        private List<INamedTypeSymbol> GetAllStrongTypedId(Compilation compilation)
        {
            var list = GetStrongTypedIdFromCurrentProject(compilation);
            list.AddRange(GetStrongTypedIdFromReferences(compilation));
            return list;
        }

        private List<INamedTypeSymbol> GetStrongTypedIdFromCurrentProject(Compilation compilation)
        {
            List<INamedTypeSymbol> strongTypedIds = new();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                if (syntaxTree.TryGetText(out var sourceText) &&
                    !sourceText.ToString().Contains("StronglyTypedId"))
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var typeDeclarationSyntaxs = syntaxTree.GetRoot().DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>();
                foreach (var tds in typeDeclarationSyntaxs)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(tds);
                    if (symbol is INamedTypeSymbol namedTypeSymbol && IsStrongTypedId(namedTypeSymbol))
                    {
                        strongTypedIds.Add(namedTypeSymbol);
                    }
                }
            }

            return strongTypedIds;
        }

        private List<INamedTypeSymbol> GetStrongTypedIdFromReferences(Compilation compilation)
        {
            var refs = compilation.References.Where(p => p.Properties.Kind == MetadataImageKind.Assembly).ToList();
            List<INamedTypeSymbol> strongTypedIds = new();
            foreach (var r in refs)
            {
                if (compilation.GetAssemblyOrModuleSymbol(r) is not IAssemblySymbol assembly)
                {
                    continue;
                }

                var nameprefix = compilation.AssemblyName?.Split('.')[0];
                if (assembly.Name.StartsWith(nameprefix))
                {
                    var types = GetAllTypes(assembly);
                    strongTypedIds.AddRange(types.Where(IsStrongTypedId));
                }
            }

            return strongTypedIds;
        }

        

        private void GetTypesInNamespace(INamespaceSymbol namespaceSymbol, List<INamedTypeSymbol> types)
        {
            types.AddRange(namespaceSymbol.GetTypeMembers());
            foreach (var subNamespaceSymbol in namespaceSymbol.GetNamespaceMembers())
            {
                GetTypesInNamespace(subNamespaceSymbol, types);
            }
        }

        private bool IsStrongTypedId(INamedTypeSymbol type)
        {
            return type.TypeKind == TypeKind.Class &&
                   type.AllInterfaces.Any(p => p.Name == "IStronglyTypedId");
        }
    }
}