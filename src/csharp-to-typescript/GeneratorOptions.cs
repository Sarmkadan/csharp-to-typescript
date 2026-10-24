using System;

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
        var options = new GeneratorOptions();

        if (ReadValue(tryGetValue, nameof(OutputFile)) is string outputFile)
        {
            options.OutputFile = outputFile;
        }

        if (ReadEnum<PropertyNamingPolicy>(tryGetValue, nameof(NamingPolicy)) is PropertyNamingPolicy namingPolicy)
        {
            options.NamingPolicy = namingPolicy;
        }

        if (ReadEnum<EnumStyle>(tryGetValue, nameof(EnumStyle)) is EnumStyle enumStyle)
        {
            options.EnumStyle = enumStyle;
        }

        if (ReadBool(tryGetValue, nameof(NullableAsOptional)) is bool nullableAsOptional)
        {
            options.NullableAsOptional = nullableAsOptional;
        }

        if (ReadBool(tryGetValue, nameof(DateAsString)) is bool dateAsString)
        {
            options.DateAsString = dateAsString;
        }

        return options;
    }

    /// <summary>
    /// Reads a trimmed, non-empty value for the given option name, or <c>null</c> when absent or blank.
    /// </summary>
    private static string? ReadValue(System.Func<string, string?> tryGetValue, string name)
    {
        var value = tryGetValue("build_property." + MsBuildPrefix + name);
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static T? ReadEnum<T>(System.Func<string, string?> tryGetValue, string name)
        where T : struct
    {
        var value = ReadValue(tryGetValue, name);
        if (value is not null && System.Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? ReadBool(System.Func<string, string?> tryGetValue, string name)
    {
        var value = ReadValue(tryGetValue, name);
        if (value is not null && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
