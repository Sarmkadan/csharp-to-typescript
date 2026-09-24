using System;

namespace CSharpToTypeScript;

/// <summary>
/// Excludes a property or field from the generated TypeScript declaration.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class TypeScriptIgnoreAttribute : Attribute
{
}
