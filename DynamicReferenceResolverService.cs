using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Compiler;

public static class DynamicReferenceResolverService
{

    // When compiling dynamic code, Roslyn needs to resolve symbols, but using SyntaxTree models
    // makes it difficult to find all referenced assemblies. By implement SemanticModel 
    /// we can discover all assemblies that are referenced in the source code.

    public static List<MetadataReference> DiscoverAll(string sourceCode,
        string bootstrapAssemblyName = "Bootstrap")
    {
        
        // Framework / BCL references for base types, LINQ, etc. so to create Compilation object 
        // that can resolve symbols in the source code by using the TPA set.
        var tpaRefs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();

        // Create a minimal compilation so Roslyn can bind symbols
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var bootstrap = CSharpCompilation.Create(
            bootstrapAssemblyName,
            new[] { syntaxTree },
            tpaRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = bootstrap.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

        // Walk the tree and collect every resolved symbol’s assembly
        var assemblyLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add the TPA set first (no duplicates later)
        foreach (var r in tpaRefs) assemblyLocations.Add(r.Display);

        // Create a simple walker
        foreach (var nodeOrToken in syntaxTree.GetRoot().DescendantNodesAndTokens())
        {
            ISymbol? symbol = nodeOrToken.IsNode
                ? model.GetSymbolInfo(nodeOrToken.AsNode()!).Symbol
                  ?? model.GetDeclaredSymbol(nodeOrToken.AsNode()!)
                : null;

            if (symbol is null || symbol.Kind == SymbolKind.Namespace)
                continue; 

            var asm = symbol.ContainingAssembly;
            if (asm == null) continue; 

            // Try to find that assembly among the already-loaded ones
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => !a.IsDynamic &&
                                     string.Equals(a.GetName().Name,
                                         asm.Name,
                                         StringComparison.Ordinal));

            if (loaded != null && !string.IsNullOrEmpty(loaded.Location))
                assemblyLocations.Add(loaded.Location);
        }

        // Final MetadataReference list
        return assemblyLocations
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
    }
}