# Finding code by feature

Application behavior is organized by purpose under `Features/<Purpose>` in the API,
SharedWeb, and WebClient projects. Matching tests use the same purpose names. Projects
still represent runtime and dependency boundaries; a feature can span those boundaries.

| Purpose | Look here for |
| --- | --- |
| Authoring | The author's workspace, drafts, story/book/chapter editing, covers, tagging a work, publishing and unpublishing |
| Reading | Published content delivery, the reader, chapter navigation, progress, qualified reads, comments, and Markdown responses |
| Discovery | Home and browse pages, public story and author details, search, tag landing pages, public catalog queries, and sitemap generation |
| Following | Following authors or stories, notification inboxes, push subscriptions, devices, and notification fan-out |
| AccountManagement | Registration as an author, personal and author profile settings, and sign-in UI |
| Administration | Administrative UI and canonical tag maintenance/reconciliation |
| AgentAccess | Agent authentication, read-only access policy, claims, and machine-facing API/auth discovery |

These are the current capabilities, not a closed list or a resource ownership rule.
Choose a location by the outcome someone is trying to change. A feature may read or
modify several resources. Using the logged-in user's identity does not make an operation
an account feature: private draft reads belong to Authoring, while a personal notification
inbox belongs to Following. Public story metadata belongs to Discovery; delivering its
readable body belongs to Reading. HTTP URLs need not mirror source folders.

## Keep a use case together

Each API use case has one descriptively named file containing its request and response
types, validation, errors, handling, and nested endpoint. Keep existing attribute-based
validation; the layout does not require a new validation framework or generic Command /
Handler abstractions. A queued implementation used only by that use case belongs in the
same file. Public message and handler types may remain top-level where framework discovery
requires them. Services or handlers shared by multiple use cases stay outside individual
use-case files, within the purpose they serve.

In SharedWeb, colocate feature pages, components, service facades, models, CSS, and
JavaScript modules. Do not split a feature back into global Pages, Components, Services,
Commands, or Handlers folders. Use a descriptive subfolder when a cohesive group needs
one, such as Authoring/MarkdownEditor; do not create an empty hierarchy for every use case.
Feature-specific WebClient middleware lives under the same purpose as the behavior it
supports. Cross-feature calls are allowed; duplicating services or data to make folders
independent is not a goal.

Authoring's `AuthorWorkspaceService` owns private story listings and draft content
retrieval. Editing and publishing facades consume those reads rather than duplicating
them. Reading uses `WorkService` / `IPublishedWorkReader` for published bodies; do not
add unused story/chapter facades just to mirror resource names.

AgentAccess protocol operations have named use-case files, such as
`RegisterAgentIdentity`, `ConfirmAgentIdentityClaim`, and `ExchangeAgentToken`, with
nested endpoints registered by the normal endpoint discovery mechanism. Shared protocol
errors, scopes, and the rate-limited route-group factory live in `AgentAuthProtocol`.

## Shared code and compatibility

- Keep shared entities, persistence mappings, contexts, migrations, and cross-cutting
  primitives in their existing data/kernel projects. A table does not define a feature.
- Keep genuinely reusable UI, layout, metadata composition, browser storage, theme,
  transport, and host composition outside Features. Shared locations require actual
  reuse or a cross-cutting responsibility, not uncertainty about where to put a file.
- Namespaces follow project-relative folders, including Razor namespaces and test
  namespaces. Update imports, fully qualified references, isolated CSS/JS paths, and
  source-path references together when moving files.
- Preserve HTTP routes, operation IDs, schema names, authorization, and generated-client
  behavior during layout-only changes. Generate contracts through the normal build.
- Preserve durable message identities explicitly when moving message namespaces. The
  legacy-looking aliases on queued messages are compatibility contracts, not layout drift.
- Review feature placement against this map and the changed behavior. Guidance and code
  review enforce semantic placement; there is no automated folder taxonomy checker.
