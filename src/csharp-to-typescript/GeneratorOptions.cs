namespace CSharpToTypeScript;

/// <summary>
/// Naming policy applied to member names when emitting TypeScript.
/// </summary>
public enum PropertyNamingPolicy
{
    /// <summary>Keep the C# member name unchanged.</summary>
    Unchanged = 0,

    /// <summary>Convert <c>FirstName</c> to <c>firstName</c>. This is the default.</summary>
    CamelCase = 1,

    /// <summary>Convert <c>FirstName</c> to <c>first_name</c>.</summary>
    SnakeCase = 2,
}

/// <summary>
/// How C# enums are represented in TypeScript.
/// </summary>
public enum EnumStyle
{
    /// <summary>Emit <c>export enum Color { Red = 0, Green = 1 }</c>.</summary>
    NumericEnum = 0,

    /// <summary>Emit <c>export enum Color { Red = "Red", Green = "Green" }</c>.</summary>
    StringEnum = 1,

    /// <summary>Emit <c>export type Color = "Red" | "Green";</c>.</summary>
    StringUnion = 2,
}

/// <summary>
/// Generator-wide options resolved from MSBuild properties (<c>CSharpToTypeScript_*</c>)
/// and <c>.editorconfig</c> <c>build_property</c> entries.
/// </summary>
public sealed class GeneratorOptions
{
    /// <summary>Default MSBuild property prefix.</summary>
    public const string MsBuildPrefix = "CSharpToTypeScript_";

    /// <summary>
    /// Gets the default output path for generated declarations, relative to the project directory.
    /// Defaults to <c>Generated/types.ts</c>.
    /// </summary>
    public string OutputFile { get; set; } = "Generated/types.ts";

    /// <summary>Gets the member naming policy. Defaults to <see cref="PropertyNamingPolicy.CamelCase"/>.</summary>
    public PropertyNamingPolicy NamingPolicy { get; set; } = PropertyNamingPolicy.CamelCase;

    /// <summary>Gets the enum emission style. Defaults to <see cref="EnumStyle.StringUnion"/>.</summary>
    public EnumStyle EnumStyle { get; set; } = EnumStyle.StringUnion;

    /// <summary>
    /// Gets a value indicating whether nullable reference and value types are emitted as optional members
    /// (<c>name?: T</c>) instead of <c>name: T | null</c>.
    /// </summary>
    public bool NullableAsOptional { get; set; } = false;

    /// <summary>Gets a value indicating whether <see cref="System.DateTime"/> and friends map to <c>string</c> (ISO 8601) rather than <c>Date</c>.</summary>
    public bool DateAsString { get; set; } = true;

    /// <summary>
    /// Creates options from a flat key/value bag as supplied by
    /// <c>AnalyzerConfigOptionsProvider.GlobalOptions</c>.
    /// </summary>
    /// <param name="tryGetValue">Lookup delegate; keys are passed with the <c>build_property.</c> prefix.</param>
    /// <returns>Resolved options with defaults for absent keys.</returns>
    public static GeneratorOptions FromAnalyzerConfig(System.Func<string, string?> tryGetValue)
    {
        throw new System.NotImplementedException();
    }
}
