using Microsoft.CodeAnalysis;

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
        throw new System.NotImplementedException();
    }
}
