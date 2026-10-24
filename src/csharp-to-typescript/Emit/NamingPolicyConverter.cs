using System;
using System.Text;

namespace CSharpToTypeScript.Emit;

/// <summary>
/// Converts C# member names to the casing selected by a <see cref="PropertyNamingPolicy"/>.
/// </summary>
public static class NamingPolicyConverter
{
    /// <summary>
    /// Applies <paramref name="policy"/> to <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The C# member name.</param>
    /// <param name="policy">The naming policy to apply.</param>
    /// <returns>
    /// The converted name. <see cref="PropertyNamingPolicy.Unchanged"/> returns the input as-is;
    /// <see cref="PropertyNamingPolicy.CamelCase"/> lowers the leading upper-case run (<c>FirstName</c> to
    /// <c>firstName</c>, <c>URLValue</c> to <c>urlValue</c>, <c>ID</c> to <c>id</c>);
    /// <see cref="PropertyNamingPolicy.SnakeCase"/> splits on case and digit-to-letter boundaries and joins
    /// with <c>_</c> (<c>HTTPStatus</c> to <c>http_status</c>, <c>Item2Value</c> to <c>item2_value</c>).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="policy"/> is not a defined value.</exception>
    public static string Apply(string name, PropertyNamingPolicy policy)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (!Enum.IsDefined(typeof(PropertyNamingPolicy), policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown naming policy.");
        }

        switch (policy)
        {
            case PropertyNamingPolicy.Unchanged:
                return name;
            case PropertyNamingPolicy.CamelCase:
                return ToCamelCase(name);
            case PropertyNamingPolicy.SnakeCase:
                return ToSnakeCase(name);
            default:
                throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown naming policy.");
        }
    }

    private static string ToCamelCase(string name)
    {
        var upperRun = 0;
        while (upperRun < name.Length && char.IsUpper(name[upperRun]))
        {
            upperRun++;
        }

        if (upperRun == 0)
        {
            return name;
        }

        // In "URLValue" the last upper-case letter starts the next word, so it keeps its case.
        // A single leading capital ("FirstName") is always lowered.
        var lowerCount = upperRun;
        if (upperRun > 1 && upperRun < name.Length && char.IsLower(name[upperRun]))
        {
            lowerCount = upperRun - 1;
        }

        var builder = new StringBuilder(name.Length);
        for (var i = 0; i < lowerCount; i++)
        {
            builder.Append(char.ToLowerInvariant(name[i]));
        }

        builder.Append(name, lowerCount, name.Length - lowerCount);
        return builder.ToString();
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && IsWordBoundary(name, i))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns <see langword="true"/> when a new word starts at <paramref name="index"/>, which must be greater than zero.
    /// </summary>
    private static bool IsWordBoundary(string name, int index)
    {
        var current = name[index];
        var previous = name[index - 1];

        if (char.IsUpper(current))
        {
            // "itemName" -> "item_name", "2Value" -> "2_value"
            if (char.IsLower(previous) || char.IsDigit(previous))
            {
                return true;
            }

            // "HTTPStatus": the last capital of an acronym starts a word when a lower-case letter follows.
            return char.IsUpper(previous) && index + 1 < name.Length && char.IsLower(name[index + 1]);
        }

        // "Item2Value", "item2value": a letter after a digit starts a new word.
        return char.IsLetter(current) && char.IsDigit(previous);
    }
}
