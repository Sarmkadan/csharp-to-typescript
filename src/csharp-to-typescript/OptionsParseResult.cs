using System.Collections.Generic;

namespace CSharpToTypeScript;

/// <summary>
/// Result of <see cref="GeneratorOptions.Parse"/>: the resolved options plus every <c>CSharpToTypeScript_*</c>
/// value that was present but could not be parsed.
/// </summary>
public sealed class OptionsParseResult
{
    internal OptionsParseResult(GeneratorOptions options, IReadOnlyList<(string Key, string RawValue, string Default)> invalidValues)
    {
        Options = options;
        InvalidValues = invalidValues;
    }

    /// <summary>Gets the resolved options. Invalid values are replaced by their defaults.</summary>
    public GeneratorOptions Options { get; }

    /// <summary>
    /// Gets one entry per invalid value: the MSBuild property name (e.g. <c>CSharpToTypeScript_NamingPolicy</c>),
    /// the trimmed value that was supplied, and the default that was used instead. Empty when every value was valid.
    /// </summary>
    public IReadOnlyList<(string Key, string RawValue, string Default)> InvalidValues { get; }
}
