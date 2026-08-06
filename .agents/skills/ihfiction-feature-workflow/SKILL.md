---
name: ihfiction-feature-workflow
description: Implement, plan, or review complete vertical feature slices in the IHeartFiction repository. Use for product features that can cross domain entities, PostgreSQL or MongoDB persistence, Minimal APIs and OpenAPI-generated clients, Keycloak authorization, WolverineFx messaging, shared Blazor UI, accessibility, metadata, and automated tests. Also use when a change seems small but agents need to identify every affected layer with minimal repository rediscovery.
---

# IHeartFiction Feature Workflow

Build the smallest complete feature slice while preserving this repository's architecture and guardrails. Route to specialized skills only when the feature actually needs them.

## Start with bounded context

1. Read the repository `AGENTS.md`. Its commands and guardrails override companion skills.
2. Restate the user-visible outcome, roles, routes, data, failure cases, and explicit non-goals.
3. Read [references/architecture-map.md](references/architecture-map.md), then locate the nearest existing analogue with its commands.
4. Classify the feature using [references/feature-routing.md](references/feature-routing.md). Do not load every listed companion skill.
5. Create an impact checklist containing only applicable rows from [references/completeness-gates.md](references/completeness-gates.md).
6. Search the repository's GitHub Discussions for the existing roadmap or feature-planning thread that covers the work. Record one canonical discussion URL and its current status; update that discussion instead of creating a duplicate. Treat it as stakeholder-facing planning context, not as a substitute for repository evidence.

When the user authorizes GitHub writes, synchronize the canonical discussion when implementation meaningfully starts, when scope or status materially changes, and after verification. Keep updates concise, distinguish delivered work from planned or deferred work, and never mark a feature complete before its applicable gates pass. If GitHub writes are unavailable or outside the request's authority, prepare the exact proposed discussion update for handoff instead.

If requirements materially change behavior, use `spec-driven-development` before implementation. For a narrow, already-specified change, proceed without manufacturing a separate specification.

## Implement a vertical slice

Work in coherent, testable increments:

1. Define or update domain behavior and errors.
2. Add persistence and mapping only where the owning data store requires it.
3. Add the use case and endpoint contract, including validation, authorization, response metadata, and links.
4. Refresh and consume the OpenAPI contract when the API surface changes.
5. Add the shared Blazor service, component, or page behavior.
6. Add asynchronous messages and handlers only when the Wolverine decision says they are warranted.
7. Add tests at the cheapest level that proves each behavior; use integration or browser tests for boundaries unit tests cannot prove.
8. Run the applicable completeness gates before calling the slice done.

Keep each increment end-to-end enough to compile and verify. Preserve unrelated user changes and avoid opportunistic refactors.

## Enforce repository decisions

- Treat PostgreSQL/EF Core as the owner of relational metadata and MongoDB driver collections as the owner of document bodies where existing analogues do so. Do not introduce a second source of truth.
- Use WolverineFx for queued, asynchronous, cross-domain, or horizontally scalable work. Keep immediate local work synchronous, and do not migrate existing code merely for consistency.
- For queued HTTP mutations, keep boundary validation synchronous, publish resolved identifiers and timestamps rather than `HttpContext` or `ClaimsPrincipal`, return `202 Accepted`, and make the handler idempotent against distinct envelopes for the same client action. Keep canonical rows and denormalized counters in one handler transaction unless temporary divergence and projection repair are deliberate.
- Treat Redis as Wolverine's transport and PostgreSQL as the durable inbox/outbox/local-queue store. Configure explicit streams and consumer groups for scale-out handlers, durable inboxes on listeners, durable outboxes on senders, native dead-letter streams where appropriate, and durable local queues by policy.
- On Wolverine 6.x, provision Redis streams/groups and relational envelope storage with `opts.Services.AddResourceSetupOnStartup()`. `MapWolverineEnvelopeStorage(...)` integrates envelope entities with an EF unit of work but deliberately excludes those tables from EF migrations; do not keep or force an empty EF migration for them or enable competing EF-managed Wolverine migrations.
- Prefer source-generated `[LoggerMessage]` partial methods for structured C# logging.
- Keep authorization on the server even when the UI also hides or disables an action. Distinguish anonymous, authenticated, author, owner, collaborator, and device flows.
- Treat API C# as contract source, `openapi.json` as generated input, and the SharedWeb typed client as a downstream consumer. Never hand-edit generated client output.
- Use the exact EF migration command from `AGENTS.md`. When schema errors follow model changes, verify the AppHost and migration service before debugging higher layers.
- When PostgreSQL resilience is enabled, never start a user transaction outside the configured EF execution strategy. Wrap the complete atomic unit in `Database.CreateExecutionStrategy().ExecuteInTransactionAsync(...)`, use `SaveChangesAsync(acceptAllChangesOnSuccess: false, ...)`, provide a database verifier for ambiguous commit outcomes, and call `ChangeTracker.AcceptAllChanges()` only after confirmed success. Keep per-attempt mutable state inside the strategy delegate so retries cannot reuse accumulated deltas. Use `ConvertStoryType` as the repository analogue.
- If a Wolverine handler owns that resilient transaction, configure EF integration with `TransactionMiddlewareMode.Lightweight`; eager middleware opens a user transaction before the execution strategy and reproduces Npgsql's unsupported-transaction failure. Domain uniqueness and merge rules must still tolerate redelivery because inbox deduplication covers an envelope ID, not separate HTTP retries that publish new envelopes.
- In Interactive Server components, invoke JS only after interactive rendering from `OnAfterRenderAsync`. Treat disconnects during initialization and disposal as normal races by handling `JSDisconnectedException`; do not depend on teardown interop for client cleanup. Attach `ElementReference` values to the stable rendered element being observed, not loading or error branches. At the protected-browser-storage boundary, treat `CryptographicException` as stale data after a data-protection key change: delete the unreadable key and return the missing-value result instead of terminating the circuit.
- Preserve `SocialPreviewMetadata.razor` as the single SEO `HeadContent` owner and follow the repository's section-composition and route verification rules.
- Check `.agents/WORKAROUNDS.md` before removing unusual build plumbing, package overrides, suppressions, or generated-contract edges.

## Verify proportionally

Run focused tests after each increment and broader checks after integration. Before any `--no-restore` build/test command, run the required agent bootstrap. For API contract changes, perform a normal host-runtime API build so `openapi.json` is refreshed, then build the consumer that invokes client generation.

Bootstrap reuses any `.artifacts/packages/IHFiction.SourceGenerators*.nupkg` by default. After source-generator changes, force publication with `agent-bootstrap.ps1 -ForceSourceGeneratorPackage` or `agent-bootstrap.sh --force-source-generator-package`; Bash publication requires `python3` for normalized NuGet package comparison. On Windows, use PowerShell for the actual preflight when Git Bash cannot see `dotnet`, and use Ubuntu WSL to validate Bash behavior. If WSL lacks a Linux SDK, translate every WSL path passed to Windows `dotnet.exe` with `wslpath -w`; a raw `/mnt/...` project path is parsed by MSBuild as an invalid switch.

Use Aspire resource commands for the running topology. `restart` does not compile source; use the resource's `rebuild` command. Because WebClient, FictionApi, and SharedWeb share build outputs on Windows, stop every running consumer that holds assemblies before rebuilding. If a resource reports down but its process still owns a DLL, recover with `aspire stop` and `aspire start`, then rebuild and wait for each resource to become healthy. Inspect `aspire describe` for the current resource names and supported commands rather than assuming generated names.

For integration hosts that construct Wolverine during application startup, provide PostgreSQL and Redis connection strings through `IWebHostBuilder.UseSetting(...)` before host construction; replacing DbContext services later is too late. Exercise Redis with a real Testcontainer, use its `Hostname` and mapped port with `abortConnect=false`, dispose the `WebApplicationFactory` and Wolverine agents before stopping PostgreSQL/Redis containers, and clean up only verified orphaned fixed-name test containers after interrupted runs.

For a new queued route, do not stop at direct handler tests. Verify boundary validation publishes nothing on failure, valid input publishes the minimal message, repeated handler delivery preserves domain counts, the OpenAPI/client contract accepts `202`, and a live Aspire request traverses the Redis listener and eventually changes authoritative database state. Poll a read model with a bounded timeout instead of asserting an immediate update.

For Interactive Server failures, reproduce the exact route in Playwright and correlate the same run with `aspire otel logs` and traces. Capture page errors, console errors, failed requests, loading/error UI state, redirects, and the relevant API response. Exercise stale protected storage and qualification timers explicitly when they are part of the behavior. Treat a healthy resource or HTTP 200 as insufficient if the Blazor circuit can still fail after hydration.

Do not claim browser behavior, metadata, authentication, migration application, or distributed messaging is verified from compilation alone. Use the relevant Aspire, integration, or browser-testing skill and report anything that remains unverified.

## Report the result

Summarize:

- the user-visible outcome;
- the layers changed and important design choice;
- the verification performed and result;
- any migration, generated artifact, deployment, or manual browser step still required.
- the canonical GitHub Discussion link and whether it was synchronized or has a proposed update awaiting authorization.

Do not dump the full checklist unless unresolved items make it useful.
