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
