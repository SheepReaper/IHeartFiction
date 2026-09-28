using System.Diagnostics.CodeAnalysis;

namespace IHFiction.SharedKernel.AgentAuth;

public static class AuthMdContent
{
    public static string Generate(Uri resourceUri, Uri agentIssuerUri, Uri keycloakAuthorityUri)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);
        ArgumentNullException.ThrowIfNull(agentIssuerUri);
        ArgumentNullException.ThrowIfNull(keycloakAuthorityUri);
        return Generate(resourceUri.ToString(), agentIssuerUri.ToString(), keycloakAuthorityUri.ToString());
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Overload with System.Uri is provided")]
    public static string Generate(string resourceUrl, string authorityUrl) =>
        Generate(resourceUrl, resourceUrl, authorityUrl);

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Overload with System.Uri is provided")]
    public static string Generate(string resourceUrl, string agentIssuerUrl, string keycloakAuthorityUrl)
    {
        ArgumentNullException.ThrowIfNull(resourceUrl);
        ArgumentNullException.ThrowIfNull(agentIssuerUrl);
        ArgumentNullException.ThrowIfNull(keycloakAuthorityUrl);

        var resource = resourceUrl.TrimEnd('/');
        var issuer = agentIssuerUrl.TrimEnd('/');
        var keycloak = keycloakAuthorityUrl.TrimEnd('/');

        return $$"""
# auth.md - IHeartFiction agent authentication

IHeartFiction supports agent-verified registration using an Identity Assertion JWT Authorization Grant (ID-JAG). The resource server and agent authorization server are at `{{resource}}`. Interactive browser authentication remains at `{{keycloak}}`.

## 1. Discover

1. Read `WWW-Authenticate: Bearer resource_metadata="{{resource}}/.well-known/oauth-protected-resource"` from a 401 response, or fetch that URL directly.
2. Fetch {{issuer}}/.well-known/oauth-authorization-server and read its `agent_auth` block.
3. Fetch signing keys from `{{issuer}}/.well-known/jwks.json`.

Supported registration type: `identity_assertion`.

Supported scopes:

- `agent.read`: read API resources using the registered user's identity.
- `profile.read`: read the registered user's own profile.

Agent credentials cannot create or modify content, follow resources, change profiles, or acquire author or administrator roles.

## 2. Register an identity assertion

Send a short-lived ID-JAG from a configured trusted provider:

```http
POST {{issuer}}/agent/identity
Content-Type: application/json

{
  "type": "identity_assertion",
  "assertion_type": "urn:ietf:params:oauth:token-type:id-jag",
  "assertion": "<provider-signed ID-JAG>"
}
```

The assertion must be audience-bound to `{{issuer}}`, contain a fresh `auth_time`, a unique `jti`, and a verified email. A successful response contains a service-signed `identity_assertion`, its expiration, and approved scopes.

If the verified email belongs to an existing account that is not yet linked to the provider identity, the endpoint returns `401 interaction_required` with a `claim_token`, `user_code`, and `verification_uri`. Show the URI and code to the user. The user signs in to IHeartFiction and confirms the code there; do not ask the user to send the code back to the agent.

## 3. Exchange the assertion

```http
POST {{issuer}}/oauth2/token
Content-Type: application/x-www-form-urlencoded

grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion=<service-signed-identity-assertion>&resource={{resource}}
```

For a pending first-link claim, poll the same endpoint no faster than the advertised interval:

```http
POST {{issuer}}/oauth2/token
Content-Type: application/x-www-form-urlencoded

grant_type=urn:workos:agent-auth:grant-type:claim&claim_token=<claim-token>&resource={{resource}}
```

The claim grant returns `authorization_pending` until confirmation and `slow_down` when polled too quickly. On success it returns an access token and identity assertion. No refresh token is issued.

## 4. Use and revoke the access token

```http
Authorization: Bearer <access-token>
```

Access tokens last at most one hour. Revoke one by posting its value as `token` to `{{issuer}}/oauth2/revoke`. When an assertion or registration is expired or revoked, restart registration.

## Errors

- `invalid_issuer`, `invalid_signature`, `invalid_audience`, `invalid_client_id`: the provider assertion is not trusted or valid.
- `missing_verified_email`: the assertion cannot be matched safely.
- `replay_detected`: the assertion or event `jti` was already used.
- `login_required`: the provider authentication is too old.
- `interaction_required`: the existing user must confirm the first link.
- `authorization_pending`, `slow_down`, `expired_token`, `invalid_grant`: the claim or token exchange cannot complete yet.
""";
    }
}
