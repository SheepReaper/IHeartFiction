# IHFiction.SourceGenerators

Local analyzer package for IHeartFiction source generators.

This package is restored from the repository-local NuGet feed and loaded by consuming projects as a Roslyn analyzer/source generator. It is not intended for publication to nuget.org.

Included generators:

- Endpoint registration generation for `IHFiction.FictionApi`.
- OpenAPI client generation for `IHFiction.SharedWeb` when `openapi.json` is provided as an additional file.
