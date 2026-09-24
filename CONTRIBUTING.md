# Contributing

1. Open an issue before large changes so the design can be discussed.
2. Fork, branch from `main`, keep commits focused. Conventional commit prefixes (`feat:`, `fix:`, `test:`, `docs:`).
3. `dotnet build` must pass with zero warnings (`TreatWarningsAsErrors` is on).
4. Every behaviour change needs a test in `tests/csharp-to-typescript.Tests`. Generator tests use snapshot-style
   assertions on emitted `.ts` text; keep expected output in the test file, not in external fixtures.
5. Public API needs XML docs.
6. Open a PR; CI must be green.

Do not add `Co-Authored-By` trailers.
