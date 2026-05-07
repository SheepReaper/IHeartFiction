# Agent Command Guardrails

## EF Core Migration Creation (Mandatory)

For this repository, the migration creation command is fixed.

Use exactly:

```bash
dotnet ef migrations add <MigrationName> --project src/lib/IHFiction.Data/IHFiction.Data.csproj --context FictionDbContext
```

Do not use any variant for standard migration creation, including:

- Any `--project` path other than `.\src\lib\IHFiction.Data\IHFiction.Data.csproj`

If the wrong command is run, rerun immediately with the exact command above and report the correction.

## WolverineFx Guidance (Mandatory For New Work)

For new features in this repository, WolverineFx is the primary mechanism when asynchronous, queued, or cross-domain messaging behavior is needed.

Use WolverineFx when:

- API requests should return immediately while work is processed asynchronously.
- Work crosses service/domain boundaries and should be modeled as messages/events.
- Queued work should be processed in parallel by multiple instances.

Do not refactor existing features solely to adopt WolverineFx unless explicitly requested.
