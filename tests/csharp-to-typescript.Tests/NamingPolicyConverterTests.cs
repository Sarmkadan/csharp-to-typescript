using CSharpToTypeScript.Emit;

namespace CSharpToTypeScript.Tests;

public class NamingPolicyConverterTests
{
    [Theory]
    [InlineData("FirstName", "firstName")]
    [InlineData("ID", "id")]
    [InlineData("URLValue", "urlValue")]
    [InlineData("X", "x")]
    [InlineData("firstName", "firstName")]
    [InlineData("Item2", "item2")]
    [InlineData("_private", "_private")]
    public void Apply_CamelCase_ConvertsName(string input, string expected)
    {
        Assert.Equal(expected, NamingPolicyConverter.Apply(input, PropertyNamingPolicy.CamelCase));
    }

    [Theory]
    [InlineData("FirstName", "first_name")]
    [InlineData("HTTPStatus", "http_status")]
    [InlineData("Item2Value", "item2_value")]
    [InlineData("ID", "id")]
    [InlineData("already_snake", "already_snake")]
    [InlineData("A", "a")]
    public void Apply_SnakeCase_ConvertsName(string input, string expected)
    {
        Assert.Equal(expected, NamingPolicyConverter.Apply(input, PropertyNamingPolicy.SnakeCase));
    }

    [Theory]
    [InlineData("FirstName")]
    [InlineData("URLValue")]
    [InlineData("Item2Value")]
    [InlineData("already_snake")]
    [InlineData("_private")]
    public void Apply_Unchanged_ReturnsInputVerbatim(string input)
    {
        Assert.Equal(input, NamingPolicyConverter.Apply(input, PropertyNamingPolicy.Unchanged));
    }

    [Theory]
    [InlineData(PropertyNamingPolicy.Unchanged)]
    [InlineData(PropertyNamingPolicy.CamelCase)]
    [InlineData(PropertyNamingPolicy.SnakeCase)]
    public void Apply_EmptyString_ReturnsEmpty(PropertyNamingPolicy policy)
    {
        Assert.Equal(string.Empty, NamingPolicyConverter.Apply(string.Empty, policy));
    }

    [Theory]
    [InlineData(PropertyNamingPolicy.Unchanged)]
    [InlineData(PropertyNamingPolicy.CamelCase)]
    [InlineData(PropertyNamingPolicy.SnakeCase)]
    public void Apply_NullName_Throws(PropertyNamingPolicy policy)
    {
        Assert.Throws<ArgumentNullException>(() => NamingPolicyConverter.Apply(null!, policy));
    }

    [Fact]
    public void Apply_UndefinedPolicy_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NamingPolicyConverter.Apply("FirstName", (PropertyNamingPolicy)99));
    }
}
