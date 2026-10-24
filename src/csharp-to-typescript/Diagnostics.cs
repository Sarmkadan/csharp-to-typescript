using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript;

/// <summary>
/// Diagnostic descriptors reported by <see cref="TypeScriptGenerator"/>. Every descriptor id must also be listed
/// in <c>AnalyzerReleases.Unshipped.md</c> (or <c>AnalyzerReleases.Shipped.md</c> once released) to satisfy RS2008.
/// </summary>
internal static class Diagnostics
{
    private const string Category = "CSharpToTypeScript";
    private const string HelpLinkUri = "https://github.com/Sarmkadan/csharp-to-typescript#diagnostics";

    /// <summary>CSTS001: a type marked for export uses a construct that cannot be mapped to TypeScript.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedType = new(
        id: TypeScriptGenerator.UnsupportedTypeDiagnosticId,
        title: "Unsupported type construct",
        messageFormat: "Type '{0}' uses unsupported construct: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkUri);

    /// <summary>CSTS002: two exported types resolve to the same TypeScript name.</summary>
    internal static readonly DiagnosticDescriptor DuplicateName = new(
        id: TypeScriptGenerator.DuplicateNameDiagnosticId,
        title: "Duplicate TypeScript name",
        messageFormat: "TypeScript name '{0}' is produced by both '{1}' and '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkUri);

    /// <summary>CSTS003: the generated <c>.ts</c> file could not be written to disk.</summary>
    internal static readonly DiagnosticDescriptor OutputWriteFailed = new(
        id: TypeScriptGenerator.OutputWriteFailedDiagnosticId,
        title: "Output write failed",
        messageFormat: "Could not write '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkUri);

    /// <summary>CSTS004: a <c>CSharpToTypeScript_*</c> MSBuild property has a value that cannot be parsed.</summary>
    internal static readonly DiagnosticDescriptor InvalidOption = new(
        id: TypeScriptGenerator.InvalidOptionDiagnosticId,
        title: "Invalid option value",
        messageFormat: "Option '{0}' has invalid value '{1}', using default '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkUri);

    /// <summary>
    /// Creates a <see cref="Diagnostic"/> for the given descriptor, filling its message placeholders with <paramref name="args"/>.
    /// </summary>
    /// <param name="descriptor">Descriptor to instantiate.</param>
    /// <param name="location">Location to report at, or <c>null</c> for a location-less diagnostic.</param>
    /// <param name="args">Values for the descriptor's message placeholders, in order.</param>
    /// <returns>The diagnostic ready to be reported through the source production context.</returns>
    internal static Diagnostic Create(DiagnosticDescriptor descriptor, Location? location, params object[] args)
    {
        return Diagnostic.Create(descriptor, location, args);
    }
}
