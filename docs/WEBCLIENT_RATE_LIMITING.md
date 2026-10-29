# WebClient visitor identity and rate limiting

## Request identity

WebClient calls FictionApi on behalf of website visitors. Each application
processes one `X-Forwarded-For` / `X-Forwarded-Proto` hop from its configured
trusted proxies. Forwarding uses registered `ForwardedHeadersOptions` and the
framework's parameterless `UseForwardedHeaders`; host filtering remains enabled.
Automatic forwarding environment variables must remain unset. A conflicting
automatic-forwarding setting fails startup instead of being silently overwritten.

WebClient captures the validated remote IP in request-scoped state. Interactive
Server persistence encrypts and authenticates that state with Data Protection and
restores it before routed components run. The circuit retains its initial address
across interactive navigation and reconnects; a full page load captures it anew.
Do not switch this service to InteractiveAuto/WebAssembly or expose it as editable
browser state. Preserve the application's Data Protection keys.

The existing `Routes` component owns the Interactive Server render boundary.
Registered persistent services are resolved and restored by the framework; no
wrapper component or cascading visitor value is required by the HTTP handler.

The FictionApi client owns an outer handler in the caller's request/circuit scope.
It replaces `X-Forwarded-For` with the validated visitor IP. Factory-managed inner
handlers retain connection pooling without retaining visitor state. Bearer tokens
continue to use the authentication handler and API token validation. Missing
visitor context removes any supplied forwarding header, emits a warning, and uses
the API's server-IP fallback. Device IDs are application data, not trusted
rate-limit identity.

API public and qualified reads remain IP-based, including for signed-in users.
Authenticated reads and mutations retain their subject-based policies.

## WebClient policies

Configuration keys are under `RateLimiting`. All values are positive integer
permits per one-minute fixed window, partitioned by normalized visitor IP.
There is no queue and exactly one policy applies to a request.

| Key | Default | Traffic |
|---|---:|---|
| General | 300 | Dynamic pages, Markdown, cover proxy, discovery, sitemap, unknown routes |
| Authentication | 10 | Login, logout, refresh |
| Callbacks | 30 | Configured OIDC callback and sign-out paths |
| Connections | 30 | Blazor negotiation and WebSocket upgrade attempts |
| Reports | 60 | Browser/CSP reports |

Actual static-asset endpoints, health/uptime, and established SignalR transport
polls/sends/disconnects are exempt. A 429 response is plain text, includes
`Retry-After` and `Cache-Control: no-store`, and bypasses authentication callbacks,
status-page re-execution, rendering, conversion, and backend calls.

The `IHFiction.WebClient.RateLimiting` meter exports
`web.rate_limit.rejections` with a low-cardinality `policy` tag. Rejection logs do
not include visitor IPs or tokens. Monitor these alongside ASP.NET Core built-in
rate-limit metrics and API 429s.

Limits are per process. Revisit enforcement before increasing replicas. These
policies do not cap active circuits, throttle messages inside a connection, or
change the editor's message-size allowance.

## Deployment configuration

Set `Api:TrustedProxies` and `WebClient:TrustedProxies` in private AppHost production
configuration or the deployment configuration store. Both are required for
production publication. Values must be explicit CIDRs for the actual trusted
proxy networks; there is no infrastructure-specific fallback in source.

The previous shared AppHost `TrustedProxies` key is not used for these services.
Each generated service receives its own value as `TrustedProxies`. Recheck these
values after network or proxy changes. Every workload on a trusted network is
within the forwarding trust boundary.

Keep environment-specific addresses, cluster and host inventories, service
placements, image digests, operational history, and deployment backups outside
version control. Do not copy private configuration into generated files and then
check those files in. Production publication removes automatic forwarded-header
environment variables from API/WebClient; the dashboard has separate settings.
