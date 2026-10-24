using System.Linq;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript;

/// <summary>
/// Pipeline-safe snapshot of the named arguments of <c>[TypeScriptExport]</c>.
/// Holds only strings and a bool, so it can be cached by the incremental pipeline without retaining symbols.
/// </summary>
/// <param name="Name">Explicit TypeScript type name, or <see langword="null"/> when not set.</param>
/// <param name="OutputFile">Explicit output file, or <see langword="null"/> when not set.</param>
/// <param name="Export">Whether the declaration is prefixed with <c>export</c>.</param>
public sealed record ExportAttributeInfo(string? Name, string? OutputFile, bool Export)
{
    /// <summary>
    /// Reads the named arguments of a <c>[TypeScriptExport]</c> attribute. <c>Export</c> defaults to <see langword="true"/>.
    /// </summary>
    /// <param name="attribute">Attribute data whose class is <see cref="TypeScriptGenerator.ExportAttributeFullName"/>.</param>
    public static ExportAttributeInfo FromAttribute(AttributeData attribute)
    {
        string? name = null;
        string? outputFile = null;
        var export = true;

        foreach (var argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case "Name":
                    name = argument.Value.Value as string;
                    break;

                case "OutputFile":
                    outputFile = argument.Value.Value as string;
                    break;

                case "Export":
                    export = argument.Value.Value as bool? ?? true;
                    break;
            }
        }

        return new ExportAttributeInfo(name, outputFile, export);
    }

    /// <summary>
    /// Finds the <c>[TypeScriptExport]</c> attribute on <paramref name="symbol"/>.
    /// </summary>
    /// <param name="symbol">Symbol to inspect.</param>
    /// <returns>The parsed attribute, or <see langword="null"/> when the symbol is not exported.</returns>
    public static ExportAttributeInfo? TryGet(ISymbol symbol)
    {
        var attribute = symbol.GetAttributes()
            .FirstOrDefault(data => data.AttributeClass?.ToDisplayString() == TypeScriptGenerator.ExportAttributeFullName);

        return attribute is null ? null : FromAttribute(attribute);
    }
}
