using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Emit;

/// <summary>
/// Renders TypeScript source text for a set of exported symbols.
/// Output is deterministic: declarations are sorted by name and formatted with two-space indentation.
/// </summary>
public sealed class TypeScriptEmitter
{
    private readonly ITypeMapper _mapper;
    private readonly GeneratorOptions _options;

    /// <summary>Initializes a new emitter.</summary>
    /// <param name="mapper">Type mapper used for member types.</param>
    /// <param name="options">Generator options.</param>
    public TypeScriptEmitter(ITypeMapper mapper, GeneratorOptions options)
    {
        _mapper = mapper ?? throw new System.ArgumentNullException(nameof(mapper));
        _options = options ?? throw new System.ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Emits an <c>interface</c> declaration for a class, record or struct.
    /// </summary>
    /// <param name="type">Named type marked with <see cref="TypeScriptExportAttribute"/>.</param>
    /// <returns>TypeScript source for the single declaration, including trailing newline.</returns>
    public string EmitInterface(INamedTypeSymbol type)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Emits an enum declaration in the style configured by <see cref="GeneratorOptions.EnumStyle"/>.
    /// </summary>
    /// <param name="enumType">Enum symbol marked with <see cref="TypeScriptExportAttribute"/>.</param>
    /// <returns>TypeScript source for the single declaration, including trailing newline.</returns>
    public string EmitEnum(INamedTypeSymbol enumType)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Emits a complete file: auto-generated header, then every declaration in stable order.
    /// </summary>
    /// <param name="types">All exported symbols destined for one output file.</param>
    /// <returns>Full TypeScript file content.</returns>
    public string EmitFile(IReadOnlyList<INamedTypeSymbol> types)
    {
        throw new System.NotImplementedException();
    }
}
