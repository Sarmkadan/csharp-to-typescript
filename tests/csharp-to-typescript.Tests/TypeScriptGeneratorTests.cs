using System.Collections.Generic;
using System.Linq;
using CSharpToTypeScript.Emit;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Tests;

/// <summary>
/// End-to-end tests: each one runs the real generator over a consumer-style compilation and asserts on the emitted TypeScript.
/// </summary>
public class TypeScriptGeneratorTests
{
    private const string ReadmeSource = """
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

    private const string DefaultOutputPath = "Generated/types.ts";

    [Fact]
    public void Run_ReadmeSample_ProducesDocumentedTypesTs()
    {
        var run = GeneratorTestHost.Run(ReadmeSource);

        // The README block lists declarations as LineDto, OrderStatus, OrderDto. The emitter writes enums before
        // interfaces, each sorted by name, so the expected text follows the emitter order.
        Assert.Equal(DefaultOutputPath, run.Outputs.Single().Path);
        Assert.Equal(ReadmeTypesTs(), run.Content);
    }

    [Fact]
    public void Run_OnlyRootAnnotated_EmitsReferencedLineDtoAndOrderStatus()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using CSharpToTypeScript;

            [TypeScriptExport]
            public record OrderDto(Guid Id, string Customer, OrderStatus Status, List<LineDto> Lines, DateTime? ShippedAt);

            public record LineDto(string Sku, int Quantity, decimal Price);

            public enum OrderStatus { Pending, Paid, Shipped }
            """;

        var run = GeneratorTestHost.Run(source);

        Assert.Single(run.Outputs);
        Assert.Equal(ReadmeTypesTs(), run.Content);
    }

    [Fact]
    public void Run_ExplicitOutputFile_ProducesSecondSourceWithDistinctPath()
    {
        const string source = """
            using System;
            using CSharpToTypeScript;

            [TypeScriptExport]
            public record OrderDto(Guid Id, string Customer);

            [TypeScriptExport(OutputFile = "other.ts")]
            public record Ping(string Message);
            """;

        var run = GeneratorTestHost.Run(source);

        Assert.Equal(2, run.RunResult.GeneratedTrees.Length);
        Assert.Equal(2, run.Outputs.Select(output => output.Path).Distinct().Count());
        Assert.Equal(
            TypeScriptEmitter.Header + Lines(
                "",
                "export interface OrderDto {",
                "  id: string;",
                "  customer: string;",
                "}"),
            run.Output(DefaultOutputPath).Content);
        Assert.Equal(
            TypeScriptEmitter.Header + Lines(
                "",
                "export interface Ping {",
                "  message: string;",
                "}"),
            run.Output("other.ts").Content);
    }

    [Fact]
    public void Run_EnumStyleNumericEnum_EmitsNumericEnum()
    {
        const string source = """
            using CSharpToTypeScript;

            [TypeScriptExport]
            public enum OrderStatus { Pending, Paid, Shipped }

            [TypeScriptExport]
            public record OrderDto(OrderStatus Status);
            """;

        var defaultRun = GeneratorTestHost.Run(source);
        var numericRun = GeneratorTestHost.Run(source, new Dictionary<string, string>
        {
            ["CSharpToTypeScript_EnumStyle"] = "NumericEnum",
        });

        Assert.Contains("export type OrderStatus = \"Pending\" | \"Paid\" | \"Shipped\";", defaultRun.Content);
        Assert.Equal(
            TypeScriptEmitter.Header + Lines(
                "",
                "export enum OrderStatus {",
                "  Pending = 0,",
                "  Paid = 1,",
                "  Shipped = 2,",
                "}",
                "",
                "export interface OrderDto {",
                "  status: OrderStatus;",
                "}"),
            numericRun.Content);
    }

    [Fact]
    public void Run_NamingPolicySnakeCase_RenamesMembers()
    {
        var run = GeneratorTestHost.Run(ReadmeSource, new Dictionary<string, string>
        {
            ["CSharpToTypeScript_NamingPolicy"] = "SnakeCase",
        });

        Assert.Equal(
            TypeScriptEmitter.Header + Lines(
                "",
                "export type OrderStatus = \"Pending\" | \"Paid\" | \"Shipped\";",
                "",
                "export interface LineDto {",
                "  sku: string;",
                "  quantity: number;",
                "  price: number;",
                "}",
                "",
                "export interface OrderDto {",
                "  id: string;",
                "  customer: string;",
                "  status: OrderStatus;",
                "  lines: LineDto[];",
                "  shipped_at: string | null;",
                "}"),
            run.Content);
    }

    [Fact]
    public void Run_ReadmeSample_CompilationHasNoErrors()
    {
        var run = GeneratorTestHost.Run(ReadmeSource);

        // Generator diagnostics are included so a CSTS error reported by the generator also fails this test.
        var errors = run.Compilation.GetDiagnostics()
            .Concat(run.RunResult.Results.SelectMany(result => result.Diagnostics))
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void Run_SameCompilationTwice_ProducesIdenticalOutput()
    {
        var compilation = TestCompilation.Create(ReadmeSource);

        var first = GeneratorTestHost.Run(compilation);
        var second = GeneratorTestHost.Run(compilation);

        Assert.Equal(first.Outputs, second.Outputs);
        Assert.Equal(
            first.RunResult.GeneratedTrees.Select(tree => tree.ToString()),
            second.RunResult.GeneratedTrees.Select(tree => tree.ToString()));
    }

    /// <summary>
    /// The README "Quick start" types.ts: enums first, then interfaces sorted by name, behind the emitter header.
    /// </summary>
    private static string ReadmeTypesTs() =>
        TypeScriptEmitter.Header + Lines(
            "",
            "export type OrderStatus = \"Pending\" | \"Paid\" | \"Shipped\";",
            "",
            "export interface LineDto {",
            "  sku: string;",
            "  quantity: number;",
            "  price: number;",
            "}",
            "",
            "export interface OrderDto {",
            "  id: string;",
            "  customer: string;",
            "  status: OrderStatus;",
            "  lines: LineDto[];",
            "  shippedAt: string | null;",
            "}");

    /// <summary>Joins lines with <c>\n</c> and newline-terminates each one.</summary>
    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + "\n"));
}
