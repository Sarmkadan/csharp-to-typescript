using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharpToTypeScript.Tests;

/// <summary>
/// Drives the real <see cref="TypeScriptGenerator"/> through <see cref="CSharpGeneratorDriver"/> so tests can assert on
/// the generated TypeScript exactly as a consumer build would see it.
/// </summary>
internal static class GeneratorTestHost
{
    private const string BuildPropertyPrefix = "build_property.";

    /// <summary>
    /// Compiles <paramref name="source"/> and runs the generator over it.
    /// </summary>
    /// <param name="source">C# source of the consuming compilation.</param>
    /// <param name="buildProps">
    /// <c>CSharpToTypeScript_*</c> MSBuild property values, keyed without the <c>build_property.</c> prefix.
    /// </param>
    public static GeneratorTestRun Run(string source, Dictionary<string, string>? buildProps = null) =>
        Run(TestCompilation.Create(source), buildProps);

    /// <summary>
    /// Runs the generator over an existing compilation. Reusing the same instance lets tests check that an unchanged
    /// compilation produces identical output on a fresh driver.
    /// </summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="buildProps">
    /// <c>CSharpToTypeScript_*</c> MSBuild property values, keyed without the <c>build_property.</c> prefix.
    /// </param>
    public static GeneratorTestRun Run(CSharpCompilation compilation, Dictionary<string, string>? buildProps = null)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new TypeScriptGenerator().AsSourceGenerator() },
            optionsProvider: new BuildPropertiesProvider(buildProps ?? new Dictionary<string, string>()));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var runResult = driver.GetRunResult();

        var outputs = runResult.GeneratedTrees
            .Select(ExtractOutput)
            .ToList();

        return new GeneratorTestRun(runResult, updated, outputs);
    }

    /// <summary>
    /// Reads the <c>Path</c> and <c>Content</c> constants from one generated <c>.g.cs</c> file.
    /// </summary>
    private static TypeScriptOutput ExtractOutput(SyntaxTree tree)
    {
        var root = tree.GetRoot();

        return new TypeScriptOutput(ReadStringConstant(root, "Path"), ReadStringConstant(root, "Content"));
    }

    /// <summary>
    /// Returns the value of a <c>const string</c> field declared with a plain literal, e.g. <c>public const string Path = @"..."</c>.
    /// </summary>
    private static string ReadStringConstant(SyntaxNode root, string fieldName)
    {
        var variable = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(field => field.Declaration.Variables)
            .Single(candidate => candidate.Identifier.ValueText == fieldName);

        var literal = variable.Initializer?.Value as LiteralExpressionSyntax
            ?? throw new InvalidOperationException($"Field '{fieldName}' is not initialised with a string literal.");

        return literal.Token.ValueText;
    }

    /// <summary>
    /// Exposes the build properties as <c>build_property.*</c> global analyzer config options, the same shape MSBuild produces.
    /// </summary>
    private sealed class BuildPropertiesProvider : AnalyzerConfigOptionsProvider
    {
        public BuildPropertiesProvider(IReadOnlyDictionary<string, string> buildProps)
        {
            GlobalOptions = new BuildProperties(
                buildProps.ToDictionary(pair => BuildPropertyPrefix + pair.Key, pair => pair.Value));
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private sealed class BuildProperties : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public BuildProperties(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}

/// <summary>One TypeScript file produced by the generator: its output path and its rendered content.</summary>
/// <param name="Path">Output path as passed to the generator, e.g. <c>Generated/types.ts</c>.</param>
/// <param name="Content">Rendered TypeScript source.</param>
internal sealed record TypeScriptOutput(string Path, string Content);

/// <summary>
/// Result of <see cref="GeneratorTestHost.Run(string, Dictionary{string, string}?)"/>.
/// </summary>
/// <param name="RunResult">Raw driver result, including generated trees and generator diagnostics.</param>
/// <param name="Compilation">The compilation after generated sources were added.</param>
/// <param name="Outputs">One entry per generated <c>.g.cs</c> file, in the order the driver returned them.</param>
internal sealed record GeneratorTestRun(
    GeneratorDriverRunResult RunResult,
    Compilation Compilation,
    IReadOnlyList<TypeScriptOutput> Outputs)
{
    /// <summary>The TypeScript content of the only output. Throws when the run produced zero or several outputs.</summary>
    public string Content => Outputs.Single().Content;

    /// <summary>The output written to <paramref name="path"/>.</summary>
    public TypeScriptOutput Output(string path) => Outputs.Single(output => output.Path == path);
}
