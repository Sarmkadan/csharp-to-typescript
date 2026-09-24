using System;

namespace CSharpToTypeScript;

/// <summary>
/// Marks a C# class, record, struct or enum for export to TypeScript.
/// The source generator emits a matching <c>interface</c>, <c>type</c> or <c>enum</c>
/// declaration into the configured output file at build time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class TypeScriptExportAttribute : Attribute
{
    /// <summary>
    /// Gets or sets an explicit TypeScript type name. When <see langword="null"/>, the C# type name is used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the output file (relative to the project) that receives this type.
    /// When <see langword="null"/>, the global default from <see cref="GeneratorOptions"/> applies.
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the emitted declaration is prefixed with <c>export</c>.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool Export { get; set; } = true;
}
