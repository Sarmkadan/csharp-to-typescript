using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        var members = new List<(int Level, int Position, string CsName, string Line)>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        string? extends = null;

        // Level 0 is the type itself; inherited members come from deeper levels until an exported base takes over.
        var level = 0;
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType)
            {
                break;
            }

            if (level > 0 && HasExportAttribute(current))
            {
                extends = ResolveTypeName(current);
                break;
            }

            CollectMembers(current, level, members, seen);
            level++;
        }

        // Base members first, then declaration order within each level.
        var lines = members
            .OrderByDescending(member => member.Level)
            .ThenBy(member => member.Position)
            .ThenBy(member => member.CsName, System.StringComparer.Ordinal)
            .Select(member => member.Line)
            .ToList();

        var builder = new StringBuilder();
        if (IsExported(type))
        {
            builder.Append("export ");
        }

        builder.Append("interface ").Append(ResolveTypeName(type));
        if (extends is not null)
        {
            builder.Append(" extends ").Append(extends);
        }

        if (lines.Count == 0)
        {
            builder.Append(" {}\n");
            return builder.ToString();
        }

        builder.Append(" {\n");
        foreach (var line in lines)
        {
            builder.Append(line).Append('\n');
        }

        builder.Append("}\n");
        return builder.ToString();
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

    /// <summary>
    /// Adds the public instance properties and fields declared directly on <paramref name="owner"/> to <paramref name="members"/>.
    /// A name already in <paramref name="seen"/> is skipped, so a derived member hides or overrides the base one.
    /// </summary>
    private void CollectMembers(
        INamedTypeSymbol owner,
        int level,
        List<(int Level, int Position, string CsName, string Line)> members,
        HashSet<string> seen)
    {
        foreach (var member in owner.GetMembers())
        {
            if (!IsPublicInstanceMember(member) || member.IsImplicitlyDeclared || !seen.Add(member.Name))
            {
                continue;
            }

            if (IsIgnored(member))
            {
                continue;
            }

            ITypeSymbol memberType;
            NullableAnnotation annotation;
            if (member is IPropertySymbol property)
            {
                memberType = property.Type;
                annotation = property.NullableAnnotation;
            }
            else
            {
                memberType = ((IFieldSymbol)member).Type;
                annotation = NullableAnnotation.None;
            }

            var name = NamingPolicyConverter.Apply(member.Name, _options.NamingPolicy);
            var optional = _options.NullableAsOptional && _mapper.IsNullable(memberType, annotation);
            var mapped = _mapper.Map(memberType, annotation);

            var position = member.Locations.FirstOrDefault(location => location.IsInSource)?.SourceSpan.Start ?? 0;
            var line = $"  {name}{(optional ? "?" : string.Empty)}: {mapped};";
            members.Add((level, position, member.Name, line));
        }
    }

    private static bool IsPublicInstanceMember(ISymbol member)
    {
        switch (member)
        {
            case IPropertySymbol property:
                return !property.IsStatic
                    && !property.IsIndexer
                    && property.DeclaredAccessibility == Accessibility.Public
                    && property.GetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.NotApplicable };

            case IFieldSymbol field:
                return !field.IsStatic
                    && !field.IsConst
                    && field.DeclaredAccessibility == Accessibility.Public;

            default:
                return false;
        }
    }

    private static bool IsIgnored(ISymbol member) =>
        member.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "CSharpToTypeScript.TypeScriptIgnoreAttribute");

    private static AttributeData? FindExportAttribute(INamedTypeSymbol type) =>
        type.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == TypeScriptGenerator.ExportAttributeFullName);

    private static bool HasExportAttribute(INamedTypeSymbol type) => FindExportAttribute(type) is not null;

    /// <summary>
    /// Returns the <c>Export</c> named argument of <c>[TypeScriptExport]</c>, defaulting to <see langword="true"/>.
    /// </summary>
    private static bool IsExported(INamedTypeSymbol type) =>
        GetExportNamedArgument(type, "Export") is not bool export || export;

    /// <summary>
    /// Returns the <c>Name</c> named argument of <c>[TypeScriptExport]</c>, falling back to the C# type name.
    /// </summary>
    private static string ResolveTypeName(INamedTypeSymbol type) =>
        GetExportNamedArgument(type, "Name") is string { Length: > 0 } name ? name : type.Name;

    private static object? GetExportNamedArgument(INamedTypeSymbol type, string argumentName)
    {
        var attribute = FindExportAttribute(type);
        if (attribute is null)
        {
            return null;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == argumentName)
            {
                return argument.Value.Value;
            }
        }

        return null;
    }
}
