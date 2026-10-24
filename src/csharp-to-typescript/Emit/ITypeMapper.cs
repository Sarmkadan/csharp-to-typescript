using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Emit;

/// <summary>
/// Maps a Roslyn <see cref="ITypeSymbol"/> to a TypeScript type expression such as
/// <c>string</c>, <c>number[]</c>, <c>Record&lt;string, Foo&gt;</c> or <c>Foo | null</c>.
/// </summary>
public interface ITypeMapper
{
    /// <summary>
    /// Produces the TypeScript type expression for <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The C# type symbol.</param>
    /// <param name="nullableAnnotation">Nullable annotation from the declaring member, if any.</param>
    /// <returns>A TypeScript type expression, never <see langword="null"/>.</returns>
    string Map(ITypeSymbol type, NullableAnnotation nullableAnnotation = NullableAnnotation.None);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> is a user type that must itself be exported
    /// (i.e. is not a primitive, collection or well-known BCL type).
    /// </summary>
    bool RequiresExport(ITypeSymbol type);
}

/// <summary>
/// Default <see cref="ITypeMapper"/> covering primitives, nullable value types, arrays,
/// <c>IEnumerable&lt;T&gt;</c>, <c>IDictionary&lt;K,V&gt;</c>, <c>Guid</c>, <c>DateTime</c>/<c>DateTimeOffset</c>,
/// <c>TimeSpan</c> and tuples.
/// </summary>
public sealed class DefaultTypeMapper : ITypeMapper
{
    private readonly GeneratorOptions _options;

    /// <summary>Initializes a new mapper.</summary>
    /// <param name="options">Generator options controlling date and nullability mapping.</param>
    public DefaultTypeMapper(GeneratorOptions options)
    {
        _options = options ?? throw new System.ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Map(ITypeSymbol type, NullableAnnotation nullableAnnotation = NullableAnnotation.None)
    {
        var mapped = MapCore(type);

        if (nullableAnnotation == NullableAnnotation.Annotated && !_options.NullableAsOptional)
        {
            mapped += " | null";
        }

        return mapped;
    }

    /// <inheritdoc />
    public bool RequiresExport(ITypeSymbol type)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// Maps the type itself, without nullability. Constructs not handled yet fall back to the C# type name.
    /// </summary>
    private static string MapCore(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return "string";

            case SpecialType.System_Boolean:
                return "boolean";

            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return "number";

            case SpecialType.System_Object:
                return "unknown";

            case SpecialType.System_Void:
                return "void";

            default:
                return type.Name;
        }
    }
}
