using CSharpToTypeScript.Emit;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Tests;

public class TypeScriptEmitterInterfaceTests
{
    [Fact]
    public void EmitInterface_Record_EmitsPublicPropertiesOnly()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            record LineDto(string Sku, int Quantity, decimal Price);
            """;

        var expected = """
            export interface LineDto {
              sku: string;
              quantity: number;
              price: number;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "LineDto"));
    }

    [Fact]
    public void EmitInterface_IgnoredProperty_IsOmitted()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            class UserDto
            {
                public int Id { get; set; }
                [TypeScriptIgnore]
                public string PasswordHash { get; set; } = "";
                public string Email { get; set; } = "";
            }
            """;

        var expected = """
            export interface UserDto {
              id: number;
              email: string;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "UserDto"));
    }

    [Fact]
    public void EmitInterface_ExplicitName_RenamesInterface()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport(Name = "Order")]
            class OrderDto
            {
                public int Id { get; set; }
            }
            """;

        var expected = """
            export interface Order {
              id: number;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "OrderDto"));
    }

    [Fact]
    public void EmitInterface_ExportFalse_DropsExportPrefix()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport(Export = false)]
            class InternalDto
            {
                public int Id { get; set; }
            }
            """;

        var expected = """
            interface InternalDto {
              id: number;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "InternalDto"));
    }

    [Fact]
    public void EmitInterface_SnakeCasePolicy_RendersSnakeCaseNames()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            class PersonDto
            {
                public string FirstName { get; set; } = "";
            }
            """;

        var expected = """
            export interface PersonDto {
              first_name: string;
            }

            """;

        var options = new GeneratorOptions { NamingPolicy = PropertyNamingPolicy.SnakeCase };

        Assert.Equal(expected, EmitInterface(source, "PersonDto", options));
    }

    [Fact]
    public void EmitInterface_NullableAsOptional_OmitsNullUnion()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            class ShipmentDto
            {
                public string? ShippedAt { get; set; }
            }
            """;

        var defaultExpected = """
            export interface ShipmentDto {
              shippedAt: string | null;
            }

            """;

        var optionalExpected = """
            export interface ShipmentDto {
              shippedAt?: string;
            }

            """;

        Assert.Equal(defaultExpected, EmitInterface(source, "ShipmentDto"));
        Assert.Equal(
            optionalExpected,
            EmitInterface(source, "ShipmentDto", new GeneratorOptions { NullableAsOptional = true }));
    }

    [Fact]
    public void EmitInterface_DerivedFromExportedBase_ExtendsBaseAndSkipsBaseMembers()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            class BaseDto
            {
                public long Id { get; set; }
            }

            [TypeScriptExport]
            class OrderDto : BaseDto
            {
                public string Code { get; set; } = "";
            }
            """;

        var expected = """
            export interface OrderDto extends BaseDto {
              code: string;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "OrderDto"));
    }

    [Fact]
    public void EmitInterface_DerivedFromNonExportedBase_InlinesBaseMembers()
    {
        const string source = """
            using CSharpToTypeScript;

            class BaseEntity
            {
                public int Id { get; set; }
            }

            [TypeScriptExport]
            class OrderDto : BaseEntity
            {
                public string Code { get; set; } = "";
            }
            """;

        var expected = """
            export interface OrderDto {
              id: number;
              code: string;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "OrderDto"));
    }

    [Fact]
    public void EmitInterface_StaticAndConstMembers_AreSkipped()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            class ConfigDto
            {
                public const int MaxItems = 10;
                public static readonly int Shared = 1;
                public static string Version { get; set; } = "1";
                public int Size { get; set; }
            }
            """;

        var expected = """
            export interface ConfigDto {
              size: number;
            }

            """;

        Assert.Equal(expected, EmitInterface(source, "ConfigDto"));
    }

    private static string EmitInterface(string source, string typeName, GeneratorOptions? options = null)
    {
        options ??= new GeneratorOptions();

        var compilation = TestCompilation.Create(source);
        var type = compilation.GetTypeByMetadataName(typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the compilation.");

        var emitter = new TypeScriptEmitter(new DefaultTypeMapper(options), options);

        return emitter.EmitInterface(type).Replace("\r", string.Empty);
    }
}
