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
- Prefer source-generated `[LoggerMessage]` partial methods for structured C# logging.
- Keep authorization on the server even when the UI also hides or disables an action. Distinguish anonymous, authenticated, author, owner, collaborator, and device flows.
- Treat API C# as contract source, `openapi.json` as generated input, and the SharedWeb typed client as a downstream consumer. Never hand-edit generated client output.
- Use the exact EF migration command from `AGENTS.md`. When schema errors follow model changes, verify the AppHost and migration service before debugging higher layers.
- Preserve `SocialPreviewMetadata.razor` as the single SEO `HeadContent` owner and follow the repository's section-composition and route verification rules.
- Check `.agents/WORKAROUNDS.md` before removing unusual build plumbing, package overrides, suppressions, or generated-contract edges.

## Verify proportionally

Run focused tests after each increment and broader checks after integration. Before any `--no-restore` build/test command, run the required agent bootstrap. For API contract changes, perform a normal host-runtime API build so `openapi.json` is refreshed, then build the consumer that invokes client generation.

Do not claim browser behavior, metadata, authentication, migration application, or distributed messaging is verified from compilation alone. Use the relevant Aspire, integration, or browser-testing skill and report anything that remains unverified.

## Report the result

Summarize:

- the user-visible outcome;
- the layers changed and important design choice;
- the verification performed and result;
- any migration, generated artifact, deployment, or manual browser step still required.

Do not dump the full checklist unless unresolved items make it useful.
