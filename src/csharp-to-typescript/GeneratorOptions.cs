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

    private string _outputFile = "Generated/types.ts";
    private PropertyNamingPolicy _namingPolicy = PropertyNamingPolicy.CamelCase;
    private EnumStyle _enumStyle = EnumStyle.StringUnion;

    /// <summary>
    /// Gets or sets the default output path for generated declarations, relative to the project directory.
    /// Defaults to <c>Generated/types.ts</c>. Backslashes are normalised to <c>/</c>; rooted paths and <c>..</c>
    /// segments are accepted.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is <see langword="null"/>, empty or white space, or contains a character from
    /// <see cref="System.IO.Path.GetInvalidPathChars"/>.
    /// </exception>
    public string OutputFile
    {
        get => _outputFile;
        set
        {
            if (!IsValidOutputFile(value))
            {
                throw new ArgumentException(
                    "Output file must not be null, empty or white space, and must not contain invalid path characters.",
                    nameof(value));
            }

            _outputFile = value.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Gets the consuming project directory (MSBuild <c>ProjectDir</c>, read from <c>build_property.projectdir</c>).
    /// When set, the generator writes each output file to disk under this directory; when <c>null</c>, it only adds
    /// the generated C# source.
    /// </summary>
    public string? ProjectDir { get; set; }

    /// <summary>Gets or sets the member naming policy. Defaults to <see cref="PropertyNamingPolicy.CamelCase"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="PropertyNamingPolicy"/>.</exception>
    public PropertyNamingPolicy NamingPolicy
    {
        get => _namingPolicy;
        set
        {
            if (!System.Enum.IsDefined(typeof(PropertyNamingPolicy), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown naming policy.");
            }

            _namingPolicy = value;
        }
    }

    /// <summary>Gets or sets the enum emission style. Defaults to <see cref="EnumStyle.StringUnion"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="EnumStyle"/>.</exception>
    public EnumStyle EnumStyle
    {
        get => _enumStyle;
        set
        {
            if (!System.Enum.IsDefined(typeof(EnumStyle), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown enum style.");
            }

            _enumStyle = value;
        }
    }

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
    /// <exception cref="ArgumentNullException"><paramref name="tryGetValue"/> is <see langword="null"/>.</exception>
    public static GeneratorOptions FromAnalyzerConfig(System.Func<string, string?> tryGetValue)
    {
        if (tryGetValue is null)
        {
            throw new ArgumentNullException(nameof(tryGetValue));
        }

        var options = new GeneratorOptions();

        // Values read from MSBuild that fail validation are ignored and the default kept, the same as unparsable enums.
        if (ReadValue(tryGetValue, nameof(OutputFile)) is string outputFile && IsValidOutputFile(outputFile))
        {
            options.OutputFile = outputFile;
        }

        // ProjectDir is a standard MSBuild property, not a CSharpToTypeScript_* one, so it is read by its raw key.
        if (tryGetValue("build_property.projectdir") is string projectDir && projectDir.Trim().Length > 0)
        {
            options.ProjectDir = projectDir.Trim();
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

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a non-blank path without characters from
    /// <see cref="System.IO.Path.GetInvalidPathChars"/>.
    /// </summary>
    private static bool IsValidOutputFile(string? value) =>
        value is not null && !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0;

    private static T? ReadEnum<T>(System.Func<string, string?> tryGetValue, string name)
        where T : struct
    {
        var value = ReadValue(tryGetValue, name);
        if (value is not null
            && System.Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            && System.Enum.IsDefined(typeof(T), parsed))
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
