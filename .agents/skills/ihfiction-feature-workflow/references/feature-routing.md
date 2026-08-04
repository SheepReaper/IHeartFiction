# Feature routing

Select only the routes that apply. Explicitly recording “not applicable” for a risky seam is enough; do not invoke its skill.

| Feature signal | Required decision or action | Useful companion skills |
|---|---|---|
| New domain concept or relational relationship | Define invariants, ownership, EF mapping, migration need | `add-entity`, `dotnet-best-practices`, `ef-core` |
| New command/query or endpoint | Follow the existing use-case/nested-endpoint convention; define validation, errors, links, and OpenAPI metadata | `add-feature`, `aspnet-minimal-api-openapi`, `api-and-interface-design` |
| Background, fan-out, cross-domain, long-running, or scale-out work | Model an idempotent message/handler and decide delivery/failure behavior | `csharp-wolverinefx`, `observability-and-instrumentation` |
| Immediate local mutation/query | Keep direct in-process logic unless requirements demand messaging | `add-feature` |
| Story/work body or document-shaped content | Confirm MongoDB ownership, indexes, atomicity, and PostgreSQL reference consistency | `mongodb-schema-design`, `mongodb-connection`; use optimizer only for an actual query problem |
| Relational metadata/query | Confirm EF mapping, tracking mode, constraints, indexes, and migration | `ef-core`, `testcontainers-integration-tests` |
| New or changed HTTP contract | Refresh OpenAPI, verify generated client shape, update SharedWeb caller | `aspnet-minimal-api-openapi`, `api-and-interface-design` |
| New page/component or changed interaction | Implement loading, empty, error, success, responsive, keyboard, and theme states | `blazor-expert`, `frontend-ui-engineering`, `bulma-styling-governance`, `accessibility` |
| New palette or semantic visual state | Choose accessible tokens before Bulma class mapping | `better-colors`, `design-system-patterns`, then `bulma-styling-governance` |
| Public discoverable route/content | Review title, canonical, OG/Twitter, JSON-LD, robots, sitemap, and pagination | `seo`, `accessibility`; obey the `HeadContent` guardrail |
| Authentication, ownership, moderation, uploads, markdown, or user input | Threat-model authorization and validation at the server boundary | `security-and-hardening` |
| New behavior or bug fix | Test behavior first at the narrowest useful level | `test-driven-development`, `add-tests` |
| Real infrastructure boundary | Exercise with PostgreSQL/MongoDB containers or Aspire | `testcontainers-integration-tests`, `aspire-integration-testing` |
| Browser-only behavior, metadata, auth redirect, JS interop, or responsive UI | Verify in a real browser | `playwright-blazor-testing`, `browser-testing-with-devtools` |
| Cross-cutting or risky change | Break into independently verifiable increments and review pending changes | `incremental-implementation`, `ca-review`, `code-review-and-quality` |

## Mandatory design questions

Answer only those relevant to the feature:

1. Who may see and perform it? Is owner/collaborator enforcement separate from the `author` policy?
2. Which store owns each field? Is consistency across PostgreSQL and MongoDB required, and what happens on partial failure?
3. Must the caller wait, or should Wolverine process work after acknowledgement? How is repeat delivery made safe?
4. Does the HTTP contract need links, paging, filtering, sorting, shaping, examples, or a new typed-client method?
5. Which UI states and routes change? Are unauthenticated, unauthorized, empty, loading, error, and success states covered?
6. Does public content change metadata, sitemap, discoverability, notifications, follows, or reader progress?
7. Which test level proves the behavior without duplicating implementation details?
