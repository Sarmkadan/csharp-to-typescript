using CSharpToTypeScript.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpToTypeScript.Tests;

public class DefaultTypeMapperTests
{
    private const string ProbeSource = """
        using System;
        using System.Collections.Generic;

        class Probe
        {
            public int A { get; set; }
            public string B { get; set; }
            public string? C { get; set; }
            public int? D { get; set; }
            public List<string> E { get; set; }
            public Dictionary<string, int> F { get; set; }
            public Dictionary<int, string> G { get; set; }
            public Guid H { get; set; }
            public DateTime I { get; set; }
            public (int, string) J { get; set; }
            public string?[] K { get; set; }
            public IReadOnlyList<Guid> L { get; set; }
            public byte[] M { get; set; }
            public object N { get; set; }
            public OrderStatus O { get; set; }
        }

        enum OrderStatus { X }
        """;

    private static readonly CSharpCompilation ProbeCompilation = TestCompilation.Create(ProbeSource);

    [Theory]
    [InlineData("A", "number")]
    [InlineData("B", "string")]
    [InlineData("C", "string | null")]
    [InlineData("D", "number | null")]
    [InlineData("E", "string[]")]
    [InlineData("F", "Record<string, number>")]
    [InlineData("G", "Record<number, string>")]
    [InlineData("H", "string")]
    [InlineData("I", "string")]
    [InlineData("J", "[number, string]")]
    [InlineData("K", "(string | null)[]")]
    [InlineData("L", "string[]")]
    [InlineData("M", "string")]
    [InlineData("N", "unknown")]
    [InlineData("O", "OrderStatus")]
    public void Map_Probe_ProducesExpectedTypeScript(string propertyName, string expected)
    {
        Assert.Equal(expected, MapProperty(ProbeCompilation, propertyName, new GeneratorOptions()));
    }

    [Fact]
    public void Map_DateTime_IsDateWhenDateAsStringDisabled()
    {
        var options = new GeneratorOptions { DateAsString = false };

        Assert.Equal("Date", MapProperty(ProbeCompilation, "I", options));
    }

    [Fact]
    public void Map_NullableAsOptional_OmitsNullUnion()
    {
        var options = new GeneratorOptions { NullableAsOptional = true };

        Assert.Equal("string", MapProperty(ProbeCompilation, "C", options));
        Assert.Equal("number", MapProperty(ProbeCompilation, "D", options));
    }

    [Fact]
    public void IsNullable_AnnotatedReference_ReturnsTrue()
    {
        var mapper = new DefaultTypeMapper(new GeneratorOptions());
        var property = TestCompilation.GetProperty(ProbeCompilation, "Probe", "C");

        Assert.True(mapper.IsNullable(property.Type, property.NullableAnnotation));
    }

    [Fact]
    public void IsNullable_NullableValueType_ReturnsTrue()
    {
        var mapper = new DefaultTypeMapper(new GeneratorOptions());
        var property = TestCompilation.GetProperty(ProbeCompilation, "Probe", "D");

        Assert.True(mapper.IsNullable(property.Type, property.NullableAnnotation));
    }

    [Fact]
    public void IsNullable_NonNullableMember_ReturnsFalse()
    {
        var mapper = new DefaultTypeMapper(new GeneratorOptions());
        var property = TestCompilation.GetProperty(ProbeCompilation, "Probe", "B");

        Assert.False(mapper.IsNullable(property.Type, property.NullableAnnotation));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    [InlineData("F")]
    [InlineData("G")]
    [InlineData("H")]
    [InlineData("I")]
    [InlineData("J")]
    [InlineData("K")]
    [InlineData("L")]
    [InlineData("M")]
    [InlineData("N")]
    public void RequiresExport_BclType_ReturnsFalse(string propertyName)
    {
        var mapper = new DefaultTypeMapper(new GeneratorOptions());
        var property = TestCompilation.GetProperty(ProbeCompilation, "Probe", propertyName);

        Assert.False(mapper.RequiresExport(property.Type));
    }

    [Fact]
    public void RequiresExport_UserEnum_ReturnsTrue()
    {
        var mapper = new DefaultTypeMapper(new GeneratorOptions());
        var property = TestCompilation.GetProperty(ProbeCompilation, "Probe", "O");

        Assert.True(mapper.RequiresExport(property.Type));
    }

    [Fact]
    public void GetReferencedUserTypes_ListOfEnum_YieldsEnum()
    {
        var compilation = TestCompilation.Create(
            """
            using System.Collections.Generic;

            class Holder
            {
                public List<OrderStatus> Items { get; set; }
            }

            enum OrderStatus { X }
            """);
        var mapper = new DefaultTypeMapper(new GeneratorOptions());
        var items = TestCompilation.GetProperty(compilation, "Holder", "Items");
        var orderStatus = compilation.GetTypeByMetadataName("OrderStatus");

        var referenced = mapper.GetReferencedUserTypes(items.Type).ToList();

        var single = Assert.Single(referenced);
        Assert.Same(orderStatus, single);
    }

    private static string MapProperty(CSharpCompilation compilation, string propertyName, GeneratorOptions options)
    {
        var mapper = new DefaultTypeMapper(options);
        var property = TestCompilation.GetProperty(compilation, "Probe", propertyName);

        return mapper.Map(property.Type, property.NullableAnnotation);
    }
}
