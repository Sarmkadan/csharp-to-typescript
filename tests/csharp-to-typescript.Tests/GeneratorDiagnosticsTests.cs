using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace CSharpToTypeScript.Tests;

/// <summary>
/// Asserts on the diagnostics the generator reports through <see cref="GeneratorTestHost"/>, and checks that every
/// descriptor is documented in the analyzer release tracking files.
/// </summary>
public class GeneratorDiagnosticsTests
{
    private const string DuplicateNamespacesSource = """
        using CSharpToTypeScript;

        namespace N1
        {
            [TypeScriptExport]
            public class A { }
        }

        namespace N2
        {
            [TypeScriptExport]
            public class A { }
        }
        """;

    private const string DuplicateExplicitNameSource = """
        using CSharpToTypeScript;

        [TypeScriptExport(Name = "Shared")]
        public class Alpha { }

        [TypeScriptExport(Name = "Shared")]
        public class Beta { }
        """;

    // A constructed Wrapper<int> property is not flagged; only the open definition itself is (see TransitiveExportCollector).
    private const string OpenGenericRootSource = """
        using CSharpToTypeScript;

        [TypeScriptExport]
        public class Wrapper<T>
        {
            public T Value { get; set; }
        }
        """;

    private const string CleanSource = """
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

    private const string EnumSource = """
        using CSharpToTypeScript;

        [TypeScriptExport]
        public enum OrderStatus { Pending, Paid, Shipped }

        [TypeScriptExport]
        public record OrderDto(OrderStatus Status);
        """;

    [Fact]
    public void Run_SameNameInTwoNamespaces_ReportsOneDuplicateNameError()
    {
        var run = GeneratorTestHost.Run(DuplicateNamespacesSource);

        var duplicates = Filter(run, TypeScriptGenerator.DuplicateNameDiagnosticId);

        var duplicate = Assert.Single(duplicates);
        Assert.Equal(DiagnosticSeverity.Error, duplicate.Severity);
        Assert.Contains("N1.A", duplicate.GetMessage());
        Assert.Contains("N2.A", duplicate.GetMessage());
    }

    [Fact]
    public void Run_SameExplicitName_ReportsDuplicateName()
    {
        var run = GeneratorTestHost.Run(DuplicateExplicitNameSource);

        var duplicate = Assert.Single(Filter(run, TypeScriptGenerator.DuplicateNameDiagnosticId));
        Assert.Equal(DiagnosticSeverity.Error, duplicate.Severity);
        Assert.Contains("'Shared'", duplicate.GetMessage());
    }

    [Fact]
    public void Run_OpenGenericExportRoot_ReportsUnsupportedTypeNamingWrapper()
    {
        var run = GeneratorTestHost.Run(OpenGenericRootSource);

        var unsupported = Assert.Single(Filter(run, TypeScriptGenerator.UnsupportedTypeDiagnosticId));
        Assert.Equal(DiagnosticSeverity.Warning, unsupported.Severity);
        Assert.Contains("Wrapper", unsupported.GetMessage());
    }

    [Fact]
    public void Run_InvalidEnumStyle_ReportsInvalidOptionAndFallsBackToStringUnion()
    {
        var run = GeneratorTestHost.Run(EnumSource, new Dictionary<string, string>
        {
            ["CSharpToTypeScript_EnumStyle"] = "Banana",
        });

        var invalid = Assert.Single(Filter(run, TypeScriptGenerator.InvalidOptionDiagnosticId));
        Assert.Equal(DiagnosticSeverity.Warning, invalid.Severity);
        Assert.Contains("Banana", invalid.GetMessage());

        var output = Assert.Single(run.Outputs);
        Assert.Contains("export type OrderStatus = \"Pending\" | \"Paid\" | \"Shipped\";", output.Content);
    }

    [Fact]
    public void Run_CleanInput_ProducesNoDiagnostics()
    {
        var run = GeneratorTestHost.Run(CleanSource);

        Assert.Empty(run.RunResult.Diagnostics);
    }

    [Fact]
    public void Run_ReportedDiagnostics_HaveCstsIdsAndHelpLinks()
    {
        var runs = new[]
        {
            GeneratorTestHost.Run(DuplicateNamespacesSource),
            GeneratorTestHost.Run(DuplicateExplicitNameSource),
            GeneratorTestHost.Run(OpenGenericRootSource),
            GeneratorTestHost.Run(EnumSource, new Dictionary<string, string> { ["CSharpToTypeScript_EnumStyle"] = "Banana" }),
        };

        var diagnostics = runs.SelectMany(run => run.RunResult.Diagnostics).ToList();

        Assert.NotEmpty(diagnostics);
        foreach (var diagnostic in diagnostics)
        {
            Assert.StartsWith("CSTS", diagnostic.Id);
            Assert.False(string.IsNullOrEmpty(diagnostic.Descriptor.HelpLinkUri), $"{diagnostic.Id} has no HelpLinkUri");
        }
    }

    [Fact]
    public void Diagnostics_EveryDescriptor_IsListedInAnalyzerReleases()
    {
        var repoRoot = FindRepoRoot();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fileName in new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" })
        {
            var path = Path.Combine(repoRoot, "src", "csharp-to-typescript", fileName);
            Assert.True(File.Exists(path), $"Missing {path}");

            foreach (var line in File.ReadLines(path))
            {
                var match = RuleRowPattern.Match(line);
                if (match.Success)
                {
                    listed.Add(match.Groups["id"].Value);
                }
            }
        }

        var missing = DiagnosticDescriptors()
            .Select(descriptor => descriptor.Id)
            .Where(id => !listed.Contains(id))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>Matches a rule row such as <c>CSTS001 | CSharpToTypeScript | Warning | Unsupported type construct</c>.</summary>
    private static readonly Regex RuleRowPattern = new(@"^(?<id>CSTS\d{3})\s*\|", RegexOptions.CultureInvariant);

    private static List<Diagnostic> Filter(GeneratorTestRun run, string id) =>
        run.RunResult.Diagnostics.Where(diagnostic => diagnostic.Id == id).ToList();

    /// <summary>Every <see cref="DiagnosticDescriptor"/> field of the internal <c>Diagnostics</c> class.</summary>
    private static List<DiagnosticDescriptor> DiagnosticDescriptors()
    {
        var type = typeof(TypeScriptGenerator).Assembly.GetType("CSharpToTypeScript.Diagnostics", throwOnError: true)!;

        return type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
            .ToList();
    }

    /// <summary>Walks up from the test binaries until the directory holding the <c>.slnx</c> file is found.</summary>
    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No .slnx file found above '{AppContext.BaseDirectory}'.");
    }
}
