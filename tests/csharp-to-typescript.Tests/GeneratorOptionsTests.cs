using CSharpToTypeScript.Emit;

namespace CSharpToTypeScript.Tests;

public class GeneratorOptionsTests
{
    [Fact]
    public void FromAnalyzerConfig_NullLookup_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneratorOptions.FromAnalyzerConfig(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void OutputFile_BlankOrNull_Throws(string? value)
    {
        var options = new GeneratorOptions();

        Assert.Throws<ArgumentException>(() => options.OutputFile = value!);
    }

    [Fact]
    public void OutputFile_InvalidPathChar_Throws()
    {
        var options = new GeneratorOptions();

        Assert.Throws<ArgumentException>(() => options.OutputFile = "Generated/bad\0name.ts");
    }

    [Fact]
    public void OutputFile_BackslashesAreNormalised()
    {
        var options = new GeneratorOptions { OutputFile = @"Generated\nested\types.ts" };

        Assert.Equal("Generated/nested/types.ts", options.OutputFile);
    }

    [Theory]
    [InlineData("../shared/types.ts")]
    [InlineData("/abs/types.ts")]
    public void OutputFile_RootedOrEscaping_IsAccepted(string value)
    {
        var options = new GeneratorOptions { OutputFile = value };

        Assert.Equal(value, options.OutputFile);
    }

    [Fact]
    public void OutputFile_DefaultsToGeneratedTypes()
    {
        Assert.Equal("Generated/types.ts", new GeneratorOptions().OutputFile);
    }

    [Fact]
    public void NamingPolicy_UndefinedValue_Throws()
    {
        var options = new GeneratorOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.NamingPolicy = (PropertyNamingPolicy)99);
    }

    [Fact]
    public void EnumStyle_UndefinedValue_Throws()
    {
        var options = new GeneratorOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.EnumStyle = (EnumStyle)99);
    }

    [Fact]
    public void FromAnalyzerConfig_UndefinedNumericEnum_KeepsDefault()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_NamingPolicy"] = "99",
            ["build_property.CSharpToTypeScript_EnumStyle"] = "99",
        };

        var options = GeneratorOptions.FromAnalyzerConfig(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.Equal(PropertyNamingPolicy.CamelCase, options.NamingPolicy);
        Assert.Equal(EnumStyle.StringUnion, options.EnumStyle);
    }

    [Fact]
    public void FromAnalyzerConfig_InvalidOutputFile_KeepsDefault()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_OutputFile"] = "bad\0file.ts",
        };

        var options = GeneratorOptions.FromAnalyzerConfig(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.Equal("Generated/types.ts", options.OutputFile);
    }

    [Fact]
    public void Parse_NullLookup_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneratorOptions.Parse(null!));
    }

    [Fact]
    public void Parse_AllValid_ReportsNothing()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_NamingPolicy"] = "snakecase",
            ["build_property.CSharpToTypeScript_EnumStyle"] = "NumericEnum",
            ["build_property.CSharpToTypeScript_NullableAsOptional"] = "true",
            ["build_property.CSharpToTypeScript_DateAsString"] = "false",
            ["build_property.CSharpToTypeScript_OutputFile"] = "src/types.ts",
        };

        var result = GeneratorOptions.Parse(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.Empty(result.InvalidValues);
        Assert.Equal(PropertyNamingPolicy.SnakeCase, result.Options.NamingPolicy);
        Assert.Equal(EnumStyle.NumericEnum, result.Options.EnumStyle);
        Assert.True(result.Options.NullableAsOptional);
        Assert.False(result.Options.DateAsString);
        Assert.Equal("src/types.ts", result.Options.OutputFile);
    }

    [Fact]
    public void Parse_AbsentValues_ReportsNothing()
    {
        var result = GeneratorOptions.Parse(_ => null);

        Assert.Empty(result.InvalidValues);
        Assert.Equal(PropertyNamingPolicy.CamelCase, result.Options.NamingPolicy);
    }

    [Fact]
    public void Parse_InvalidValues_ReportsEachKeyWithRawValueAndDefault()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_NamingPolicy"] = "PascalCase",
            ["build_property.CSharpToTypeScript_EnumStyle"] = "99",
            ["build_property.CSharpToTypeScript_NullableAsOptional"] = "yes",
            ["build_property.CSharpToTypeScript_DateAsString"] = "false",
            ["build_property.CSharpToTypeScript_OutputFile"] = "  bad\0file.ts  ",
        };

        var result = GeneratorOptions.Parse(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.Equal(
            new[]
            {
                ("CSharpToTypeScript_OutputFile", "bad\0file.ts", "Generated/types.ts"),
                ("CSharpToTypeScript_NamingPolicy", "PascalCase", "CamelCase"),
                ("CSharpToTypeScript_EnumStyle", "99", "StringUnion"),
                ("CSharpToTypeScript_NullableAsOptional", "yes", "False"),
            },
            result.InvalidValues);
        Assert.Equal(PropertyNamingPolicy.CamelCase, result.Options.NamingPolicy);
        Assert.False(result.Options.DateAsString);
    }

    [Fact]
    public void FromAnalyzerConfig_InvalidBool_KeepsDefault()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_DateAsString"] = "maybe",
        };

        var options = GeneratorOptions.FromAnalyzerConfig(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.True(options.DateAsString);
    }

    [Fact]
    public void FromAnalyzerConfig_EmptyDictionary_UsesDefaults()
    {
        var options = GeneratorOptions.FromAnalyzerConfig(_ => null);

        Assert.Equal("Generated/types.ts", options.OutputFile);
        Assert.Equal(PropertyNamingPolicy.CamelCase, options.NamingPolicy);
        Assert.Equal(EnumStyle.StringUnion, options.EnumStyle);
        Assert.False(options.NullableAsOptional);
        Assert.True(options.DateAsString);
        Assert.Null(options.ProjectDir);
    }

    [Fact]
    public void FromAnalyzerConfig_BoolValueIsCaseInsensitive()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_NullableAsOptional"] = "TRUE",
            ["build_property.CSharpToTypeScript_DateAsString"] = "False",
        };

        var options = GeneratorOptions.FromAnalyzerConfig(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.True(options.NullableAsOptional);
        Assert.False(options.DateAsString);
    }

    [Theory]
    [InlineData("CSharpToTypeScript_NamingPolicy")]
    [InlineData("CSharpToTypeScript_EnumStyle")]
    [InlineData("CSharpToTypeScript_NullableAsOptional")]
    [InlineData("CSharpToTypeScript_DateAsString")]
    [InlineData("CSharpToTypeScript_OutputFile")]
    public void Parse_WhitespaceOnlyValue_KeepsDefaultAndIsNotReported(string name)
    {
        var values = new Dictionary<string, string>
        {
            ["build_property." + name] = "   ",
        };

        var result = GeneratorOptions.Parse(key => values.TryGetValue(key, out var v) ? v : null);
        var defaults = GeneratorOptions.Parse(_ => null).Options;

        Assert.Empty(result.InvalidValues);
        Assert.Equal(defaults.OutputFile, result.Options.OutputFile);
        Assert.Equal(defaults.NamingPolicy, result.Options.NamingPolicy);
        Assert.Equal(defaults.EnumStyle, result.Options.EnumStyle);
        Assert.Equal(defaults.NullableAsOptional, result.Options.NullableAsOptional);
        Assert.Equal(defaults.DateAsString, result.Options.DateAsString);
    }

    [Fact]
    public void FromAnalyzerConfig_ProjectDir_IsPopulatedAndTrimmed()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.projectdir"] = "  /src/app/  ",
        };

        var options = GeneratorOptions.FromAnalyzerConfig(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.Equal("/src/app/", options.ProjectDir);
    }

    [Fact]
    public void FromAnalyzerConfig_OutputFile_IsTrimmedAndBackslashesNormalised()
    {
        var values = new Dictionary<string, string>
        {
            ["build_property.CSharpToTypeScript_OutputFile"] = @"  Generated\nested\types.ts  ",
        };

        var options = GeneratorOptions.FromAnalyzerConfig(key => values.TryGetValue(key, out var v) ? v : null);

        Assert.Equal("Generated/nested/types.ts", options.OutputFile);
    }

    [Fact]
    public void NamingPolicyConverter_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NamingPolicyConverter.Apply(null!, PropertyNamingPolicy.CamelCase));
    }

    [Fact]
    public void NamingPolicyConverter_UndefinedPolicy_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NamingPolicyConverter.Apply("FirstName", (PropertyNamingPolicy)99));
    }
}
