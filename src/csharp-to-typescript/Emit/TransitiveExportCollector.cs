using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Emit;

/// <summary>
/// Expands the explicitly annotated export roots with every user type they reach through public instance members
/// and base types, so nested DTOs and enums are emitted even when they carry no <c>[TypeScriptExport]</c> attribute.
/// </summary>
public sealed class TransitiveExportCollector
{
    private readonly ITypeMapper _mapper;

    /// <summary>Initializes a new collector.</summary>
    /// <param name="mapper">Mapper deciding which referenced types are user types that require export.</param>
    public TransitiveExportCollector(ITypeMapper mapper)
    {
        _mapper = mapper ?? throw new System.ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Open generic definitions found during the last <see cref="Collect"/> call. They cannot be emitted as a single
    /// TypeScript declaration, so the generator reports them as CSTS001 instead.
    /// </summary>
    public IReadOnlyList<INamedTypeSymbol> Unsupported { get; private set; } = System.Array.Empty<INamedTypeSymbol>();

    /// <summary>
    /// Returns <paramref name="roots"/> followed by every user type reachable from them, in breadth-first discovery order.
    /// Duplicates and open generic definitions are excluded; the latter are listed in <see cref="Unsupported"/>.
    /// </summary>
    /// <param name="roots">Explicitly annotated root types, in input order.</param>
    /// <returns>Deterministic list of types to emit.</returns>
    public IReadOnlyList<INamedTypeSymbol> Collect(IEnumerable<INamedTypeSymbol> roots)
    {
        var result = new List<INamedTypeSymbol>();
        var unsupported = new List<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();

        // Every root is admitted before any walk starts, so discovered types always follow all roots.
        bool Admit(INamedTypeSymbol type)
        {
            if (!visited.Add(type))
            {
                return false;
            }

            if (IsOpenGeneric(type))
            {
                unsupported.Add(type);
                return false;
            }

            result.Add(type);
            return true;
        }

        void Discover(ITypeSymbol type)
        {
            foreach (var found in ReferencedUserTypes(type))
            {
                if (Admit(found))
                {
                    queue.Enqueue(found);
                }
            }
        }

        foreach (var root in roots)
        {
            if (Admit(root))
            {
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var member in current.GetMembers())
            {
                if (!IsExportableMember(member))
                {
                    continue;
                }

                var memberType = member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;
                Discover(memberType);
            }

            if (current.BaseType is { } baseType)
            {
                Discover(baseType);
            }
        }

        Unsupported = unsupported;
        return result;
    }

    /// <summary>
    /// Yields the user types referenced by <paramref name="type"/>, including <paramref name="type"/> itself. Arrays,
    /// tuples, <c>Nullable&lt;T&gt;</c> and framework generics are looked through, so <c>Foo?</c>, <c>Foo[]</c>,
    /// <c>List&lt;Foo&gt;</c> and <c>(Foo, int)</c> all yield <c>Foo</c>.
    /// </summary>
    /// <remarks>
    /// Not built on <see cref="ITypeMapper.GetReferencedUserTypes"/>, which yields only nested type arguments and
    /// would miss a plain <c>Foo</c> member. Export decisions still come from <see cref="ITypeMapper.RequiresExport"/>.
    /// </remarks>
    private IEnumerable<INamedTypeSymbol> ReferencedUserTypes(ITypeSymbol type)
    {
        IEnumerable<ITypeSymbol> children;

        switch (type)
        {
            case IArrayTypeSymbol array:
                children = new[] { array.ElementType };
                break;

            case INamedTypeSymbol { IsTupleType: true } tuple:
                children = tuple.TupleElements.Select(element => element.Type);
                break;

            case INamedTypeSymbol named:
                // Nullable<T> is transparent, so only its wrapped type can be a user type.
                if (named.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T && _mapper.RequiresExport(named))
                {
                    yield return named;
                }

                children = named.TypeArguments;
                break;

            default:
                yield break;
        }

        foreach (var child in children)
        {
            foreach (var found in ReferencedUserTypes(child))
            {
                yield return found;
            }
        }
    }

    /// <summary>
    /// Mirrors the member selection used by the emitter: public, non-static, non-indexer, non-const, not implicitly declared
    /// and not marked <c>[TypeScriptIgnore]</c>.
    /// </summary>
    private static bool IsExportableMember(ISymbol member)
    {
        if (member.IsImplicitlyDeclared || IsIgnored(member))
        {
            return false;
        }

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

    /// <summary>
    /// True for an unbound generic type or for the open definition of a generic type, e.g. <c>Box&lt;T&gt;</c> itself.
    /// </summary>
    private static bool IsOpenGeneric(INamedTypeSymbol type) =>
        type.IsUnboundGenericType || (type.IsDefinition && type.Arity > 0);
}
