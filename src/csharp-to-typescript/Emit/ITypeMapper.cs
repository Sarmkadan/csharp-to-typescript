using System.Collections.Generic;
using System.Linq;
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
    /// Returns <see langword="true"/> if a member of <paramref name="type"/> with the given annotation is nullable,
    /// i.e. it is a <c>Nullable&lt;T&gt;</c> or carries a <c>?</c> annotation. The emitter uses this to decide
    /// whether to emit an optional member (<c>name?: T</c>) when <see cref="GeneratorOptions.NullableAsOptional"/> is set.
    /// </summary>
    /// <param name="type">The C# type symbol.</param>
    /// <param name="annotation">Nullable annotation from the declaring member, if any.</param>
    bool IsNullable(ITypeSymbol type, NullableAnnotation annotation);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> is a user type that must itself be exported
    /// (i.e. is not a primitive, collection or well-known BCL type).
    /// </summary>
    bool RequiresExport(ITypeSymbol type);
}

/// <summary>
/// Default <see cref="ITypeMapper"/> covering primitives, nullable value types, arrays (<c>byte[]</c> as base64 string),
/// <c>IEnumerable&lt;T&gt;</c>, <c>IDictionary&lt;K,V&gt;</c>, tuples, enums, <c>dynamic</c>, type parameters and
/// well-known BCL types: <c>Guid</c>, <c>DateTime</c>/<c>DateTimeOffset</c>/<c>DateOnly</c>/<c>TimeOnly</c>,
/// <c>TimeSpan</c>, <c>Uri</c>, <c>JsonElement</c> and <c>JsonNode</c>.
/// </summary>
public sealed class DefaultTypeMapper : ITypeMapper
{
    /// <summary>
    /// Well-known BCL types with a fixed TypeScript mapping, keyed by the fully qualified display string (<c>ISymbol.ToDisplayString()</c>).
    /// Date/time types are handled separately because they depend on <see cref="GeneratorOptions.DateAsString"/>.
    /// </summary>
    private static readonly Dictionary<string, string> WellKnownTypes = new Dictionary<string, string>
    {
        ["System.Guid"] = "string",
        ["System.TimeSpan"] = "string",
        ["System.Uri"] = "string",
        ["System.Text.Json.JsonElement"] = "unknown",
        ["System.Text.Json.Nodes.JsonNode"] = "unknown",
    };

    /// <summary>
    /// Date/time types. Mapped to <c>string</c> (ISO 8601) when <see cref="GeneratorOptions.DateAsString"/> is set, otherwise to <c>Date</c>.
    /// </summary>
    private static readonly HashSet<string> DateTypes = new HashSet<string>
    {
        "System.DateTime",
        "System.DateTimeOffset",
        "System.DateOnly",
        "System.TimeOnly",
    };

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

        if (IsNullable(type, nullableAnnotation) && !_options.NullableAsOptional)
        {
            mapped += " | null";
        }

        return mapped;
    }

    /// <inheritdoc />
    public bool IsNullable(ITypeSymbol type, NullableAnnotation annotation) =>
        IsNullableValueType(type)
        || type.NullableAnnotation == NullableAnnotation.Annotated
        || annotation == NullableAnnotation.Annotated;

    /// <inheritdoc />
    public bool RequiresExport(ITypeSymbol type)
    {
        throw new System.NotImplementedException();
    }

    private static readonly string[] DictionaryTypes =
    {
        "System.Collections.Generic.IDictionary<TKey, TValue>",
        "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>",
    };

    private static readonly string[] EnumerableTypes =
    {
        "System.Collections.Generic.IEnumerable<T>",
        "System.Collections.Generic.ICollection<T>",
        "System.Collections.Generic.IList<T>",
        "System.Collections.Generic.IReadOnlyList<T>",
        "System.Collections.Generic.IReadOnlyCollection<T>",
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.HashSet<T>",
        "System.Collections.Generic.ISet<T>",
    };

    /// <summary>
    /// Maps the type itself, without nullability. Constructs not handled yet fall back to the C# type name.
    /// </summary>
    private string MapCore(ITypeSymbol type)
    {
        // Nullability is applied by Map(); here Nullable<T> is unwrapped to its underlying type.
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return MapCore(nullable.TypeArguments[0]);
        }

        if (type is IDynamicTypeSymbol)
        {
            return "any";
        }

        if (type is ITypeParameterSymbol)
        {
            return "unknown";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return type.Name;
        }

        // byte[] is handled before the generic array branch in MapComposite: it is transported as base64.
        if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
        {
            return "string";
        }

        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
        {
            return "[" + string.Join(", ", tuple.TupleElements.Select(element => MapElement(element.Type))) + "]";
        }

        var displayName = type.ToDisplayString();

        if (DateTypes.Contains(displayName))
        {
            return _options.DateAsString ? "string" : "Date";
        }

        if (WellKnownTypes.TryGetValue(displayName, out var wellKnown))
        {
            return wellKnown;
        }

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
                return MapComposite(type) ?? type.Name;
        }
    }

    /// <summary>
    /// Maps arrays, dictionaries and collections. Returns <see langword="null"/> if <paramref name="type"/>
    /// is none of these.
    /// </summary>
    private string? MapComposite(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return WrapElement(MapElement(array.ElementType)) + "[]";
        }

        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        if (!named.IsGenericType)
        {
            return named.ToDisplayString() == "System.Collections.IEnumerable" ? "unknown[]" : null;
        }

        // Dictionaries are enumerables too, so they have to be checked first.
        foreach (var dictionaryType in DictionaryTypes)
        {
            if (TryGetGenericInterface(named, dictionaryType, out var dictionary))
            {
                var key = dictionary.TypeArguments[0];
                var keyType = MapCore(key) == "number" ? "number" : "string";
                return $"Record<{keyType}, {MapElement(dictionary.TypeArguments[1])}>";
            }
        }

        foreach (var enumerableType in EnumerableTypes)
        {
            if (TryGetGenericInterface(named, enumerableType, out var enumerable))
            {
                return WrapElement(MapElement(enumerable.TypeArguments[0])) + "[]";
            }
        }

        return null;
    }

    /// <summary>
    /// Maps an element or value type, honouring its own nullable annotation (e.g. <c>string?</c> in <c>List&lt;string?&gt;</c>).
    /// </summary>
    private string MapElement(ITypeSymbol element) => Map(element, element.NullableAnnotation);

    private static string WrapElement(string element) => element.Contains(" | ") ? $"({element})" : element;

    private static bool IsNullableValueType(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    /// <summary>
    /// Finds a generic type or interface by its <see cref="ISymbol.OriginalDefinition"/> display string, checking
    /// <paramref name="type"/> itself first and then its <c>AllInterfaces</c>.
    /// </summary>
    /// <param name="type">The type to search.</param>
    /// <param name="metadataName">Display string of the open generic, e.g. <c>System.Collections.Generic.IList&lt;T&gt;</c>.</param>
    /// <param name="result">The matching constructed type or interface, with the type arguments of <paramref name="type"/>.</param>
    /// <returns><see langword="true"/> if a match was found.</returns>
    private static bool TryGetGenericInterface(INamedTypeSymbol type, string metadataName, out INamedTypeSymbol result)
    {
        if (type.OriginalDefinition.ToDisplayString() == metadataName)
        {
            result = type;
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() == metadataName)
            {
                result = iface;
                return true;
            }
        }

        result = null!;
        return false;
    }
}
