# Completeness gates

Create a short impact checklist from applicable sections. A feature is complete when every applicable item is implemented and verified or explicitly deferred with a reason.

## Domain and persistence

- Invariants and domain errors cover success, invalid input, missing resources, conflicts, and forbidden transitions.
- Every field has one authoritative store; PostgreSQL/MongoDB references and partial-failure behavior are deliberate.
- EF configurations, constraints, indexes, context sets, and exact-command migrations are included when the relational model changes.
- MongoDB collections, serialization, indexes, cancellation, and connection reuse follow an existing driver-based analogue.
- Concurrency, idempotency, soft deletion, timestamps, and cleanup behavior are considered where applicable.

## API, authorization, and messaging

- Request validation and sanitization occur at the boundary; response/error shapes and status codes are documented.
- Endpoint name, summary, description, tags, standard responses, media types, HATEOAS links, and route constraints match repository conventions.
- Server-side policy plus resource ownership/collaboration checks cover every mutation and private read.
- Contract changes refresh `openapi.json`; the SharedWeb generated client and callers compile against the new shape.
- Wolverine messages are public where discovery requires it, minimal, idempotent, cancellation-aware, observable, and tested for repeat/failure behavior.
- Structured logs use source-generated `LoggerMessage` methods and do not expose secrets or private story content.

## Blazor experience

- The shared service/component/page handles loading, empty, error, success, cancellation, and navigation states.
- Forms show useful validation; destructive actions require appropriate confirmation; repeated submission is safe.
- Anonymous, authenticated, author, owner, and collaborator presentations match server permissions without being relied on for security.
- Bulma-only styling follows semantic theme tokens, responsive layouts, dark/light themes, keyboard operation, focus visibility, and WCAG contrast.
- Public routes preserve canonical, OG/Twitter, JSON-LD, robots, sitemap, and pagination behavior without adding another SEO `HeadContent` owner.

## Verification

- Unit tests prove branching business behavior and errors without overspecifying implementation.
- Integration tests prove database mappings/queries, HTTP contract, authorization, and multi-store behavior where applicable.
- Wolverine tests prove message publication/handling and idempotency where applicable.
- Browser verification covers route rendering, interaction, responsive/theme states, authentication redirects, JS interop, and `document.head` when applicable.
- The required bootstrap precedes `--no-restore`; focused projects build, relevant tests pass, and generated artifacts are reviewed for freshness.
- Schema mismatches are checked against AppHost and migration-service completion before higher-layer debugging.
- Unusual plumbing is checked against `.agents/WORKAROUNDS.md` before removal.

## Scope and handoff

- No unrelated refactors or user changes are included.
- Remaining migration application, secrets, deployment, generated artifact, or manual verification steps are stated plainly.
- The final report names what was verified rather than implying compilation proved runtime behavior.
