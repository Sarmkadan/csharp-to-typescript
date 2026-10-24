using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpToTypeScript.Tests;

/// <summary>
/// Builds in-memory compilations for tests that need real Roslyn symbols.
/// </summary>
internal static class TestCompilation
{
    /// <summary>
    /// Creates a nullable-enabled compilation of <paramref name="source"/> that references the BCL and the generator assembly.
    /// </summary>
    /// <param name="source">C# source text of the compilation unit.</param>
    public static CSharpCompilation Create(string source)
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(typeof(TypeScriptExportAttribute).Assembly.Location),
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// Finds a public property declared directly on <paramref name="typeName"/>.
    /// </summary>
    /// <param name="compilation">The compilation that declares the type.</param>
    /// <param name="typeName">Metadata name of the containing type, e.g. <c>Probe</c>.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <exception cref="InvalidOperationException">The type or property does not exist.</exception>
    public static IPropertySymbol GetProperty(CSharpCompilation compilation, string typeName, string propertyName)
    {
        var type = compilation.GetTypeByMetadataName(typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the compilation.");

        return type.GetMembers(propertyName).OfType<IPropertySymbol>().Single();
    }
}
