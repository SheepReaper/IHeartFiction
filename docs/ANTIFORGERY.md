# Form submission and antiforgery

The WebClient calls `UseAntiforgery()` after authentication and authorization in
all environments. `AddRazorComponents()` supplies the framework services. The
Development environment does not disable antiforgery validation.

## Application forms

| Form | Submission | Protection |
| --- | --- | --- |
| Story search | GET to the current list route | Read-only; intentionally no antiforgery token |
| Author search | GET to the current list route | Read-only; intentionally no antiforgery token |
| Logout | POST to `/authentication/logout` | Explicit `AntiforgeryToken`; Minimal API form binding requires validation |
| Admin tag rename | Interactive Server submit event | Authenticated circuit and bearer-authenticated API; explicit POST method and `AntiforgeryToken` in the HTML form |

GET forms must not contain antiforgery tokens: successful controls are serialized
into the URL. Keep searches read-only. For new state-changing HTTP forms, use
POST and validate at the receiving endpoint, not just in the rendered markup.
Blazor `EditForm` supplies a token automatically; plain HTML POST forms need an
explicit `AntiforgeryToken`. Interactive Server event callbacks travel over the
Blazor circuit rather than submitting an HTTP form; the hidden token does not
replace circuit security or API authorization. Other editing controls currently
use these callbacks, not native HTTP forms.

FictionApi selects bearer authentication for application API requests, not the
WebClient session cookie. Its cover upload and OAuth token/revocation form
endpoints intentionally omit browser antiforgery validation. OAuth uses its own
protocol credentials. OIDC callback handling remains owned by the authentication
middleware and its state/correlation validation. Do not apply browser form-token
requirements indiscriminately to these protocols.

## Verification

`LogoutAntiforgeryTests` exercises the mapped logout endpoint over HTTP in both
Development and Production: GET is rejected; missing/invalid tokens and a token
without its matching cookie are rejected; a valid pair reaches sign-out.

Logout navigation derives its destination centrally from the routed component's
existing authorization attributes (including inheritance and `AllowAnonymous`).
Public pages preserve their local path and query; protected pages return to the
application home. `LoginDisplay` posts that destination in a hidden `returnUrl`
field. The logout endpoint independently validates local URLs using the framework
helper and falls back to the application home for invalid or missing values.

```powershell
dotnet test tests/IHFiction.UnitTests/IHFiction.UnitTests.csproj --filter FullyQualifiedName~LogoutAntiforgeryTests
```

See [Microsoft's Blazor form guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0)
and [CSRF guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
