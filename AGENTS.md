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

## Head Metadata Composition Guardrail (Mandatory)

For Blazor head metadata in this repository, treat `HeadContent` as a single-owner primitive.

Rules:

- `HeadContent` must appear only once in the effective render tree for SEO metadata composition.
- Exception: `src/lib/IHFiction.SharedWeb/Components/MarkdownEditor/Editor.razor` may continue using `HeadContent` for editor assets.
- The metadata owner component is `src/lib/IHFiction.SharedWeb/Components/SocialPreviewMetadata.razor`.
- Do not add additional page/component `HeadContent` blocks for SEO/social/JSON-LD metadata.

Why this is mandatory:

- Multiple `HeadContent` instances overwrite `HeadOutlet` output instead of appending.
- This causes silent metadata loss (OG, Twitter, canonical, JSON-LD) depending on render order.

Append strategy:

- Prefer `SectionContent` + `SectionOutlet` for append-style metadata composition.
- `SectionContent` also overwrites when the same named/keyed section is emitted more than once in the same render tree branch.
- Therefore use unique section names per concern (or guarantee single writer per section name).

InteractiveServer considerations (must check when changing metadata plumbing):

- `SectionOutlet` and corresponding `SectionContent` must participate in compatible rendering flow.
- If metadata appears in detail pages but not list/home pages, suspect render-tree placement/layout path differences.
- Validate with browser DOM inspection, not assumptions.

Required verification for metadata changes:

1. Confirm expected tags exist in `document.head` on `/`, `/stories`, `/authors`, one story detail, one chapter page, and one author detail page.
2. Confirm JSON-LD script count/types per route match expectations.
3. Confirm OG/Twitter/canonical tags remain present after structured-data additions.

## Cloud Agent Preflight (Mandatory)

Before running `dotnet build --no-restore`, `dotnet test --no-restore`, or git push/fetch commands that assume `origin`, run:

```bash
./tools/agent-bootstrap.sh
```

On Windows PowerShell, run:

```powershell
./tools/agent-bootstrap.ps1
```

Preflight guarantees:

- `origin` remote is validated and configured when it can be inferred.
- `dotnet restore` has completed so `project.assets.json` is present.
- The current .NET SDK version is printed for diagnostics.

If preflight cannot infer a remote, do not guess. Use the explicit command:

```bash
git remote add origin https://github.com/SheepReaper/IHeartFiction.git
```

## Temporary Workaround Registry (Mandatory)

Before removing unusual build targets, suppressions, or workaround-looking code, check:

```text
.agents/WORKAROUNDS.md
```

If you add a new temporary workaround, document the symptom, cause, removal criteria, and verification command there.
