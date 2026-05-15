# Agent Command Guardrails

## EF Core Migration Creation (Mandatory)

For this repository, the migration creation command is fixed.

Use exactly:

```bash
dotnet ef migrations add <MigrationName> --project src/lib/IHFiction.Data/IHFiction.Data.csproj --context FictionDbContext
```

Do not use any variant for standard migration creation, including:

- Any `--project` path other than `./src/lib/IHFiction.Data/IHFiction.Data.csproj`

If the wrong command is run, rerun immediately with the exact command above and report the correction.

## Migration Application And Schema Mismatch Guardrail (Mandatory)

Local EF migrations are applied by `src/aspire/IHFiction.MigrationService/`.

If any of these appear after model or migration changes:

- `relation ... does not exist`
- missing table, column, index, or constraint errors
- runtime failures that start immediately after schema changes

Do this first:

1. Start or verify the AppHost.
2. Start or verify the migration resource/container.
3. Wait for the migration service to finish.
4. Only then debug API, EF, LINQ, serialization, or frontend symptoms.

## WolverineFx Guidance (Mandatory For New Work)

For new features in this repository, WolverineFx is the primary mechanism when asynchronous, queued, or cross-domain messaging behavior is needed.

Use WolverineFx when:

- API requests should return immediately while work is processed asynchronously.
- Work crosses service/domain boundaries and should be modeled as messages/events.
- Queued work should be processed in parallel by multiple instances.

Do not refactor existing features solely to adopt WolverineFx unless explicitly requested.

## Logging Guidance (Preferred Pattern)

When adding or updating structured logs in C# code, prefer source-generated logging via `LoggerMessage` attributes.

Use this pattern by default:

- Mark the containing class as `partial`.
- Define `private partial void` logging methods with `[LoggerMessage(...)]` attributes.
- Do not use cached delegate fields from `LoggerMessage.Define(...)` unless explicitly required.

This aligns with analyzer expectations and keeps logging style consistent across the repository.
