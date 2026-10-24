using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CSharpToTypeScript.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpToTypeScript;

/// <summary>
/// Incremental source generator entry point. Collects every type annotated with
/// <see cref="TypeScriptExportAttribute"/>, resolves <see cref="GeneratorOptions"/> from analyzer config,
/// renders TypeScript via <see cref="Emit.TypeScriptEmitter"/> and writes the result as an additional
/// generated file. The <c>.ts</c> file is surfaced to MSBuild through the <c>CSharpToTypeScript_OutputFile</c>
/// property and copied to disk by the bundled <c>.targets</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TypeScriptGenerator : IIncrementalGenerator
{
    /// <summary>Fully-qualified metadata name of the export attribute.</summary>
    public const string ExportAttributeFullName = "CSharpToTypeScript.TypeScriptExportAttribute";

    /// <summary>Diagnostic id: type uses an unsupported C# construct (e.g. generic open type).</summary>
    public const string UnsupportedTypeDiagnosticId = "CSTS001";

    /// <summary>Diagnostic id: two exported types resolve to the same TypeScript name.</summary>
    public const string DuplicateNameDiagnosticId = "CSTS002";

    /// <summary>Diagnostic id: the generated output file could not be written to disk.</summary>
    public const string OutputWriteFailedDiagnosticId = "CSTS003";

    /// <summary>Diagnostic id: a <c>CSharpToTypeScript_*</c> option has an invalid value and the default is used.</summary>
    public const string InvalidOptionDiagnosticId = "CSTS004";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // TODO: switch to equatable model. For the first iteration the pipeline holds INamedTypeSymbol
        // instances, which breaks the CLAUDE.md caching rule; they are only read inside the output action.
        var exports = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ExportAttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax
                    or RecordDeclarationSyntax
                    or StructDeclarationSyntax
                    or EnumDeclarationSyntax
                    or InterfaceDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Collect();

        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GeneratorOptions.FromAnalyzerConfig(
                key => provider.GlobalOptions.TryGetValue(key, out var value) ? value : null));

        var combined = exports
            .Combine(options)
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, source) =>
            Generate(spc, source.Left.Left, source.Left.Right));
    }

    /// <summary>
    /// Expands the exported roots, emits one TypeScript file per output path and adds each as a generated C# source
    /// that exposes the path and content as constants. Any exception is reported as <see cref="OutputWriteFailedDiagnosticId"/>
    /// instead of failing the consumer build.
    /// </summary>
    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<INamedTypeSymbol> exports,
        GeneratorOptions options)
    {
        var target = options.OutputFile;

        try
        {
            var mapper = new DefaultTypeMapper(options);
            var collector = new TransitiveExportCollector(mapper);
            var emitter = new TypeScriptEmitter(mapper, options);

            // Sorted so the emitted output does not depend on syntax tree order.
            var roots = exports
                .OrderBy(type => type.ToDisplayString(), StringComparer.Ordinal)
                .ToList();

            var collected = collector.Collect(roots);

            foreach (var type in collector.Unsupported)
            {
                context.ReportDiagnostic(Diagnostics.Create(
                    Diagnostics.UnsupportedType,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString(),
                    "open generic definitions cannot be emitted as a single declaration"));
            }

            var groups = collected
                .GroupBy(type => OutputPathFor(type, options))
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                target = group.Key;
                ReportDuplicateNames(context, group);

                var content = emitter.EmitFile(group.ToList());
                context.AddSource(HintNameFor(target), BuildSource(target, content));
            }
        }
        catch (Exception exception)
        {
            context.ReportDiagnostic(Diagnostics.Create(
                Diagnostics.OutputWriteFailed,
                null,
                target,
                exception.Message));
        }
    }

    /// <summary>
    /// Reports <see cref="DuplicateNameDiagnosticId"/> for every type whose TypeScript name was already produced earlier in the same file.
    /// </summary>
    private static void ReportDuplicateNames(SourceProductionContext context, IEnumerable<INamedTypeSymbol> types)
    {
        var seen = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var type in types.OrderBy(type => type.ToDisplayString(), StringComparer.Ordinal))
        {
            var name = TypeScriptNameFor(type);
            if (seen.TryGetValue(name, out var first))
            {
                context.ReportDiagnostic(Diagnostics.Create(
                    Diagnostics.DuplicateName,
                    type.Locations.FirstOrDefault(),
                    name,
                    first.ToDisplayString(),
                    type.ToDisplayString()));
                continue;
            }

            seen.Add(name, type);
        }
    }

    /// <summary>Output file for a type: its own <c>OutputFile</c> attribute argument, or the global default.</summary>
    private static string OutputPathFor(INamedTypeSymbol type, GeneratorOptions options)
    {
        var outputFile = ExportAttributeInfo.TryGet(type)?.OutputFile;
        return outputFile is { Length: > 0 } ? outputFile : options.OutputFile;
    }

    /// <summary>TypeScript name for a type, matching the emitter: explicit <c>Name</c> argument or the C# name.</summary>
    private static string TypeScriptNameFor(INamedTypeSymbol type) =>
        ExportAttributeInfo.TryGet(type)?.Name is { Length: > 0 } name ? name : type.Name;

    /// <summary>Hint name made of the sanitized output path plus its hash, so distinct paths never collide.</summary>
    private static string HintNameFor(string path)
    {
        var sanitized = new string(path.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return sanitized + "_" + Hash(path) + ".g.cs";
    }

    /// <summary>Builds the C# source that exposes the output path and TypeScript content as constants.</summary>
    private static string BuildSource(string path, string content)
    {
        return new StringBuilder()
            .Append("// <auto-generated/>\n")
            .Append("namespace CSharpToTypeScript.Generated\n")
            .Append("{\n")
            .Append("    internal static class TypeScriptOutput_").Append(Hash(path)).Append('\n')
            .Append("    {\n")
            .Append("        public const string Path = @\"").Append(EscapeVerbatim(path)).Append("\";\n")
            .Append("        public const string Content = @\"").Append(EscapeVerbatim(content)).Append("\";\n")
            .Append("    }\n")
            .Append("}\n")
            .ToString();
    }

    /// <summary>Escapes a value for use inside a C# verbatim string literal.</summary>
    private static string EscapeVerbatim(string text) => text.Replace("\"", "\"\"");

    /// <summary>Stable 32-bit FNV-1a hash rendered as eight hex digits. Used instead of <c>string.GetHashCode</c>, which is randomized per process.</summary>
    private static string Hash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            return hash.ToString("x8");
        }
    }
}
