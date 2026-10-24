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
