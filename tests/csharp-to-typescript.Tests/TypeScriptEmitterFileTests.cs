using CSharpToTypeScript.Emit;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Tests;

public class TypeScriptEmitterFileTests
{
    private const string QuickStartSource = """
        using System;
        using System.Collections.Generic;
        using CSharpToTypeScript;

        [TypeScriptExport]
        public record OrderDto(Guid Id, string Customer, OrderStatus Status, List<LineDto> Lines, DateTime? ShippedAt);

        [TypeScriptExport]
        public record LineDto(string Sku, int Quantity, decimal Price);

        [TypeScriptExport]
        public enum OrderStatus { Pending, Paid, Shipped }
        """;

    // Matches the README "Quick start" Generated/types.ts block, with the header constant and a blank line after it.
    private static readonly string ExpectedQuickStart =
        TypeScriptEmitter.Header + "\n" +
        "export type OrderStatus = \"Pending\" | \"Paid\" | \"Shipped\";\n" +
        "\n" +
        "export interface LineDto {\n" +
        "  sku: string;\n" +
        "  quantity: number;\n" +
        "  price: number;\n" +
        "}\n" +
        "\n" +
        "export interface OrderDto {\n" +
        "  id: string;\n" +
        "  customer: string;\n" +
        "  status: OrderStatus;\n" +
        "  lines: LineDto[];\n" +
        "  shippedAt: string | null;\n" +
        "}\n";

    [Fact]
    public void EmitFile_QuickStartSample_MatchesReadmeOutput()
    {
        var output = EmitFile(QuickStartSource, "OrderStatus", "LineDto", "OrderDto");

        Assert.Equal(ExpectedQuickStart, output);
    }

    [Fact]
    public void EmitFile_ReversedInput_IsByteIdentical()
    {
        var compilation = TestCompilation.Create(QuickStartSource);
        var symbols = FindTypes(compilation, "OrderStatus", "LineDto", "OrderDto");
        var emitter = CreateEmitter();

        var forward = emitter.EmitFile(symbols);
        var reversed = emitter.EmitFile(symbols.AsEnumerable().Reverse().ToList());

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void EmitFile_ShuffledInput_IsByteIdentical()
    {
        var compilation = TestCompilation.Create(QuickStartSource);
        var symbols = FindTypes(compilation, "OrderStatus", "LineDto", "OrderDto");
        var emitter = CreateEmitter();

        // Fixed permutation (not Random) so a failure reproduces exactly.
        var shuffled = new List<INamedTypeSymbol> { symbols[1], symbols[2], symbols[0] };

        Assert.Equal(emitter.EmitFile(symbols), emitter.EmitFile(shuffled));
    }

    [Fact]
    public void EmitFile_DuplicateSymbol_IsEmittedOnce()
    {
        var compilation = TestCompilation.Create(QuickStartSource);
        var emitter = CreateEmitter();
        var orderDto = FindType(compilation, "OrderDto");
        var lineDto = FindType(compilation, "LineDto");
        var status = FindType(compilation, "OrderStatus");

        var output = emitter.EmitFile(new List<INamedTypeSymbol> { orderDto, orderDto, lineDto, status, lineDto });

        Assert.Equal(ExpectedQuickStart, output);
        Assert.Equal(1, CountOccurrences(output, "export interface OrderDto {"));
    }

    [Fact]
    public void EmitFile_EmptyList_EmitsOnlyHeader()
    {
        var output = CreateEmitter().EmitFile(new List<INamedTypeSymbol>());

        Assert.Equal(TypeScriptEmitter.Header, output);
    }

    [Fact]
    public void EmitFile_Output_HasNoCarriageReturnsAndEndsWithSingleNewline()
    {
        var compilation = TestCompilation.Create(QuickStartSource);
        var emitter = CreateEmitter();

        var outputs = new[]
        {
            emitter.EmitFile(FindTypes(compilation, "OrderStatus", "LineDto", "OrderDto")),
            emitter.EmitFile(new List<INamedTypeSymbol>()),
        };

        foreach (var output in outputs)
        {
            Assert.DoesNotContain('\r', output);
            Assert.EndsWith("\n", output);
            Assert.False(output.EndsWith("\n\n", StringComparison.Ordinal), "Output must end with exactly one newline.");
        }
    }

    [Fact]
    public void EmitFile_NullList_ThrowsArgumentNullException()
    {
        var emitter = CreateEmitter();

        Assert.Throws<ArgumentNullException>(() => emitter.EmitFile(null!));
    }

    [Fact]
    public void EmitFile_ListContainingNull_ThrowsArgumentException()
    {
        var compilation = TestCompilation.Create(QuickStartSource);
        var emitter = CreateEmitter();
        var symbols = new INamedTypeSymbol[] { FindType(compilation, "OrderDto"), null! };

        Assert.Throws<ArgumentException>(() => emitter.EmitFile(symbols));
    }

    private static string EmitFile(string source, params string[] typeNames)
    {
        var compilation = TestCompilation.Create(source);

        return CreateEmitter().EmitFile(FindTypes(compilation, typeNames));
    }

    private static TypeScriptEmitter CreateEmitter()
    {
        var options = new GeneratorOptions();

        return new TypeScriptEmitter(new DefaultTypeMapper(options), options);
    }

    private static List<INamedTypeSymbol> FindTypes(Compilation compilation, params string[] typeNames) =>
        typeNames.Select(name => FindType(compilation, name)).ToList();

    private static INamedTypeSymbol FindType(Compilation compilation, string typeName) =>
        compilation.GetTypeByMetadataName(typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the compilation.");

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
