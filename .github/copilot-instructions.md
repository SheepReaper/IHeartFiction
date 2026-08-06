# Copilot Instructions for IHeartFiction

IHeartFiction is a .NET 10 fiction reading and publishing platform. It uses ASP.NET Core Minimal APIs, a Blazor Web App in Interactive Server mode, .NET Aspire orchestration, PostgreSQL/EF Core for relational metadata, MongoDB for document bodies, Redis/WolverineFx for distributed messaging, and Keycloak for authentication.

Treat `AGENTS.md` as the canonical repository guardrail file. Read it before changing code, and read `.agents/WORKAROUNDS.md` before removing unusual build targets, package plumbing, suppressions, or deployment transformations. For end-to-end feature work, follow `.agents/skills/ihfiction-feature-workflow/SKILL.md` and load only the references relevant to the task.

## Start every task from repository evidence

- Preserve unrelated changes in the working tree.
- Read the files being changed, their tests, and one nearby complete analogue before designing a new pattern.
- Prefer targeted `rg` searches over broad directory reads.
- Use `Directory.Packages.props` for package versions; this solution uses Central Package Management.
- Use the SDK selected by `global.json` (currently .NET SDK 10.0.302 with `latestFeature` roll-forward). Do not substitute .NET 8 or 9.
- Repository-local `Aspire.Cli` and `dotnet-ef` versions are pinned in `.config/dotnet-tools.json` and restored by the bootstrap scripts.

## Bootstrap, build, and test

Before any `dotnet build --no-restore`, `dotnet test --no-restore`, or remote operation that assumes `origin`, run the platform-appropriate bootstrap:

```bash
./tools/agent-bootstrap.sh
```

```powershell
./tools/agent-bootstrap.ps1
```

Bootstrap restores local tools and NuGet dependencies, validates or infers `origin`, and reuses an existing `.artifacts/packages/IHFiction.SourceGenerators*.nupkg`. After changing source-generator code, force a package refresh:

```bash
./tools/agent-bootstrap.sh --force-source-generator-package
```

```powershell
./tools/agent-bootstrap.ps1 -ForceSourceGeneratorPackage
```

The Bash publication path requires `python3`. On Windows, use PowerShell for the normal bootstrap. Use Ubuntu WSL to validate Bash behavior when Git Bash cannot find `dotnet`; do not pass raw `/mnt/...` paths to Windows `dotnet.exe`/MSBuild.

CI builds Debug and tests Release. Match it with:

```bash
dotnet build --configuration Debug --no-restore
dotnet test --configuration Release --no-restore
```

Warnings are errors and full analyzers run for Debug builds. Release disables analyzers. Integration tests require Docker and use real PostgreSQL, MongoDB, and Redis Testcontainers; do not describe them as verified when Docker was unavailable.

## Run and observe the application

Prefer the repository-local Aspire CLI:

```bash
aspire start
aspire describe
```

Use `dotnet run --project src/aspire/IHFiction.AppHost` as a foreground fallback. Inspect `aspire describe` for actual resource names and supported commands. A resource `restart` does not compile changed source; use its `rebuild` command. On Windows, stop all consumers holding shared WebClient/FictionApi/SharedWeb assemblies before rebuilding. If a stopped resource still owns files, recover with `aspire stop`, `aspire start`, then rebuild.

The development migration resource is named `migrations` and is explicit-start. After an EF model/migration change, start it and wait for completion before debugging missing table, column, relation, index, or constraint failures. In non-development environments, the API waits for migration completion.

Development Keycloak imports `config/fiction-realm.json`. Configure the two AppHost parameter secrets when prompted by Aspire, or use:

```bash
dotnet user-secrets --project src/aspire/IHFiction.AppHost set Parameters:ApiKeycloakAdminClientSecret <admin-client-secret>
dotnet user-secrets --project src/aspire/IHFiction.AppHost set Parameters:ApiOidcClientSecret <frontend-client-secret>
```

Never commit secret values.

## Architecture and ownership

- `src/aspire/IHFiction.AppHost/`: runtime topology and production deployment composition.
- `src/aspire/IHFiction.MigrationService/`: PostgreSQL migration application.
- `src/IHFiction.FictionApi/<Area>/`: Minimal API vertical slices.
- `src/lib/IHFiction.Data/<Area>/Domain/` and `Configurations/`: relational domain and EF mappings.
- `src/lib/IHFiction.SharedKernel/`: shared contracts, validation, links, filtering, sorting, and shaping.
- `src/lib/IHFiction.SharedWeb/`: shared Blazor pages, components, services, metadata, and typed API client consumer.
- `src/IHFiction.WebClient/`: Interactive Server host and authentication composition.
- `tests/IHFiction.UnitTests/` and `tests/IHFiction.IntegrationTests/`: xUnit v3 tests using Microsoft.Testing.Platform.

Follow vertical slices and CQRS-lite conventions. ULIDs are the standard domain identifiers. PostgreSQL owns relational metadata; follow existing MongoDB-driver analogues for document bodies and do not create a second source of truth.

## API, persistence, and generated contracts

- Keep validation and authorization at the server boundary even when UI affordances are hidden.
- Use the shared pagination, sorting, searching, filtering, shaping, link, and validation primitives rather than duplicating them.
- Return the repository's established `ProblemDetails` and hypermedia response shapes.
- Use the exact EF migration command in `AGENTS.md`; no alternate project path is permitted.
- API C# is the contract source. `src/IHFiction.FictionApi/openapi.json` is generated input for the SharedWeb client. Never hand-edit generated client output.
- After an API contract change, perform the normal host-runtime API build that refreshes `openapi.json`, then build its SharedWeb consumer.
- When EF retry resilience and a user transaction are both required, wrap the entire unit in `Database.CreateExecutionStrategy().ExecuteInTransactionAsync(...)`; keep mutable attempt state inside the delegate and accept tracked changes only after a confirmed commit. Follow `ConvertStoryType`.

## WolverineFx and logging

Use WolverineFx for asynchronous work, cross-domain messaging, or work that should scale across instances. Keep immediate local work synchronous and do not retrofit existing features solely for consistency.

For queued HTTP mutations, validate synchronously, publish resolved identifiers/timestamps rather than request objects, return `202 Accepted`, and make handlers idempotent across distinct envelopes for the same client action. Redis Streams are the cross-instance transport; PostgreSQL owns durable inbox/outbox/local-queue storage. Preserve `AddResourceSetupOnStartup()`, `MapWolverineEnvelopeStorage(...)`, and `TransactionMiddlewareMode.Lightweight` behavior unless the task explicitly changes that architecture.

Prefer source-generated `[LoggerMessage]` partial methods. Make the containing class `partial`; do not introduce cached `LoggerMessage.Define(...)` delegates without a specific need.

## Blazor, styling, metadata, and browser verification

- The UI is Blazor Interactive Server. Invoke JavaScript only after interactive rendering from `OnAfterRenderAsync`.
- Treat `JSDisconnectedException` during initialization/disposal as a normal circuit race where appropriate.
- Use Bulma classes and the repository's theme tokens; follow the installed Bulma styling/color guidance instead of adding isolated hard-coded palettes.
- `SocialPreviewMetadata.razor` is the single SEO `HeadContent` owner. The only existing exception is `Components/MarkdownEditor/Editor.razor` for editor assets. Use uniquely owned `SectionContent`/`SectionOutlet` concerns for append-style metadata.
- Use Playwright for browser-observable changes. For Blazor, prefer `domcontentloaded` plus an explicit visible-element wait over `networkidle`. Treat `/_blazor/disconnect` failures as expected only when they correlate with navigation or circuit teardown.
- For theme checks, use `?theme=light` or `?theme=dark` unless storage persistence itself is under test.
- Metadata changes must inspect `document.head` on `/`, `/stories`, `/authors`, a story detail, chapter detail, and author detail route; verify route-appropriate JSON-LD plus canonical, Open Graph, and Twitter tags.

## Verification and handoff

Use the cheapest test that proves each behavior, then verify affected boundaries. Compilation alone does not verify browser behavior, authentication, migration application, generated API compatibility, or distributed delivery. Report the commands run, their results, anything not verified, generated artifacts/migrations produced, and any manual deployment or secret step remaining.
