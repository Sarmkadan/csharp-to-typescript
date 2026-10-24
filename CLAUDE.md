# csharp-to-typescript

Roslyn incremental source generator. Consumer marks DTOs/enums with `[TypeScriptExport]`; at build time the
generator emits a `.ts` file with matching `interface`/`enum`/`type` declarations.

## Commands

```bash
dotnet build                                  # whole solution, warnings are errors
dotnet test                                   # xunit, tests/csharp-to-typescript.Tests
dotnet pack src/csharp-to-typescript -c Release -o artifacts
```

## Layout

- `src/csharp-to-typescript/` - the generator (netstandard2.0, `IsRoslynComponent`). Packed into `analyzers/dotnet/cs`.
  - `TypeScriptGenerator.cs` - `IIncrementalGenerator` entry point, diagnostics `CSTS001`, `CSTS002`.
  - `GeneratorOptions.cs` - options resolved from MSBuild `CSharpToTypeScript_*` properties.
  - `Abstractions/` - `[TypeScriptExport]`, `[TypeScriptIgnore]` attributes (also shipped to consumers).
  - `build/` - `CSharpToTypeScript.Generator.props`, packed to `build/` and `buildTransitive/`. Declares the
    `CSharpToTypeScript_*` properties and `ProjectDir` as `CompilerVisibleProperty` (so they reach the generator as
    `build_property.*`) and sets their defaults, each guarded by `Condition="'$(Prop)' == ''"`.
  - `Emit/ITypeMapper.cs` - C# type symbol -> TS type expression.
  - `Emit/TypeScriptEmitter.cs` - renders declarations and whole files.
- `tests/csharp-to-typescript.Tests/` - xunit (net10.0). Generator tests should drive `CSharpGeneratorDriver`
  against an in-memory compilation and assert on the emitted text.
- `Directory.Build.props` - Nullable, LangVersion latest, TreatWarningsAsErrors.

## Conventions

- Generator targets netstandard2.0 only; no `ImplicitUsings`, no C# runtime features beyond what
  `LangVersion latest` polyfills syntactically. Avoid `System.Text.Json` etc. in the generator.
- Incremental pipeline: use `ForAttributeWithMetadataName`, keep pipeline models `record`/equatable so caching works.
  Never hold `ISymbol` in cached pipeline state past the transform step.
- Output must be deterministic (sorted declarations, `\n` line endings).
- Attributes live in the same assembly for now; if split into a separate `.Abstractions` package, keep
  `TypeScriptGenerator.ExportAttributeFullName` in sync.
- Tests: one file per feature area, snapshot text inline. No external fixture files.
- Commits: conventional prefixes, no `Co-Authored-By`.
