using CSharpToTypeScript.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpToTypeScript.Tests;

public class TypeScriptEmitterEnumTests
{
    private const string Source = """
        using CSharpToTypeScript;

        [TypeScriptExport]
        enum OrderStatus { Pending, Paid = 5, Shipped }

        [TypeScriptExport]
        enum Flags : byte { A = 1, B = 2 }
        """;

    [Fact]
    public void EmitEnum_StringUnion_EmitsTypeAliasOfMemberNames()
    {
        var emitter = CreateEmitter(EnumStyle.StringUnion);
        var compilation = TestCompilation.Create(Source);

        Assert.Equal(
            "export type OrderStatus = \"Pending\" | \"Paid\" | \"Shipped\";\n",
            emitter.EmitEnum(FindType(compilation, "OrderStatus")));
        Assert.Equal(
            "export type Flags = \"A\" | \"B\";\n",
            emitter.EmitEnum(FindType(compilation, "Flags")));
    }

    [Fact]
    public void EmitEnum_StringEnum_EmitsEnumWithStringValues()
    {
        var emitter = CreateEmitter(EnumStyle.StringEnum);
        var compilation = TestCompilation.Create(Source);

        var expected = """
            export enum OrderStatus {
              Pending = "Pending",
              Paid = "Paid",
              Shipped = "Shipped",
            }

            """;

        Assert.Equal(expected, emitter.EmitEnum(FindType(compilation, "OrderStatus")).Replace("\r", string.Empty));
        Assert.Equal(
            "export enum Flags {\n  A = \"A\",\n  B = \"B\",\n}\n",
            emitter.EmitEnum(FindType(compilation, "Flags")));
    }

    [Fact]
    public void EmitEnum_NumericEnum_EmitsConstantValues()
    {
        var emitter = CreateEmitter(EnumStyle.NumericEnum);
        var compilation = TestCompilation.Create(Source);

        var expected = """
            export enum OrderStatus {
              Pending = 0,
              Paid = 5,
              Shipped = 6,
            }

            """;

        Assert.Equal(expected, emitter.EmitEnum(FindType(compilation, "OrderStatus")).Replace("\r", string.Empty));
    }

    [Fact]
    public void EmitEnum_NumericEnum_ByteBackedEnum_EmitsConstantValues()
    {
        var emitter = CreateEmitter(EnumStyle.NumericEnum);
        var compilation = TestCompilation.Create(Source);

        var expected = """
            export enum Flags {
              A = 1,
              B = 2,
            }

            """;

        Assert.Equal(expected, emitter.EmitEnum(FindType(compilation, "Flags")).Replace("\r", string.Empty));
    }

    [Fact]
    public void EmitEnum_IgnoredMember_IsOmittedFromUnion()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            enum OrderStatus
            {
                Pending,
                [TypeScriptIgnore]
                Paid = 5,
                Shipped,
            }
            """;

        var emitter = CreateEmitter(EnumStyle.StringUnion);
        var compilation = TestCompilation.Create(source);

        Assert.Equal(
            "export type OrderStatus = \"Pending\" | \"Shipped\";\n",
            emitter.EmitEnum(FindType(compilation, "OrderStatus")));
    }

    [Fact]
    public void EmitEnum_EmptyEnum_StringUnion_EmitsNever()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            enum Empty { }
            """;

        var emitter = CreateEmitter(EnumStyle.StringUnion);
        var compilation = TestCompilation.Create(source);

        Assert.Equal("export type Empty = never;\n", emitter.EmitEnum(FindType(compilation, "Empty")));
    }

    [Fact]
    public void EmitEnum_ExportFalse_OmitsExportPrefix()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport(Export = false)]
            enum Internal { A }
            """;

        var emitter = CreateEmitter(EnumStyle.StringUnion);
        var compilation = TestCompilation.Create(source);

        Assert.Equal("type Internal = \"A\";\n", emitter.EmitEnum(FindType(compilation, "Internal")));
    }

    [Fact]
    public void EmitEnum_NameOverride_RenamesDeclaration()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport(Name = "OrderState")]
            enum OrderStatus { Pending }
            """;

        var emitter = CreateEmitter(EnumStyle.StringUnion);
        var compilation = TestCompilation.Create(source);

        Assert.Equal("export type OrderState = \"Pending\";\n", emitter.EmitEnum(FindType(compilation, "OrderStatus")));
    }

    [Fact]
    public void EmitEnum_ClassSymbol_ThrowsArgumentException()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            class Dto { }
            """;

        var emitter = CreateEmitter(EnumStyle.StringUnion);
        var compilation = TestCompilation.Create(source);

        Assert.Throws<ArgumentException>(() => emitter.EmitEnum(FindType(compilation, "Dto")));
    }

    [Fact]
    public void EmitEnum_Null_ThrowsArgumentNullException()
    {
        var emitter = CreateEmitter(EnumStyle.StringUnion);

        Assert.Throws<ArgumentNullException>(() => emitter.EmitEnum(null!));
    }

    private static TypeScriptEmitter CreateEmitter(EnumStyle style)
    {
        var options = new GeneratorOptions { EnumStyle = style };

        return new TypeScriptEmitter(new DefaultTypeMapper(options), options);
    }

    private static INamedTypeSymbol FindType(CSharpCompilation compilation, string typeName) =>
        compilation.GetTypeByMetadataName(typeName)
        ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the compilation.");
}
