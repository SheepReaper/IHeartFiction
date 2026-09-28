# Agent Auth: WorkOS Protocol Deviations & Architecture Record

## Overview

IHeartFiction implements agent interoperability using the open `auth.md` and `ID-JAG` (Identity Assertion JWT Profile / RFC 7523) registration paradigms pioneered by WorkOS and Cloudflare Agent Auth standards.

This document serves as a durable architectural record of where IHeartFiction's implementation conforms to, extends, or intentionally deviates from the baseline WorkOS protocol specification, and outlines future implementation TODOs.

---

## 1. Summary of Deviations

| Dimension | Standard WorkOS Protocol | IHeartFiction Implementation | Rationale / Status |
| :--- | :--- | :--- | :--- |
| **Identity Verification Medium** | Supports phone numbers (SMS / WhatsApp) & verified emails. | **Verified Email only** (`email_verified: true`). Rejects phone-only assertions. | Keycloak is the user identity store keyed by verified email. See [Phone Verification Gap](#phone-verification-gap-primary-todo) below. |
| **Server Role Separation** | Single domain often serves as both Authorization Server and Resource Server. | Dual-authority model: `FictionApi` is the Resource Server & Agent token authority; Keycloak is the Browser OpenID Connect authority. | Prevents coupling browser session cookies/OIDC flows with autonomous agent bearer tokens. |
| **Registration Flow** | Interactive browser claim redirect or auto-provisioning. | Two-tier: instant active registration for brand new users or re-logins; claim ceremony (`user_code` / polling) for existing accounts. | Prevents account hijacking when an agent presents an assertion for an existing user account without prior delegation. |
| **Token Formats & Typing** | `oauth-id-jag+jwt` (RFC 7523 / RFC 9068) and standard bearer JWTs. | Strict RFC-compliant typing: `oauth-id-jag+jwt` for service assertions, `at+jwt` for access tokens, `secevent+jwt` for RISC/revocation events. | Enforces strict validation parameters and prevents cross-token substitution attacks via `JsonWebTokenHandler`. |
| **Scope Hierarchy** | Configurable read/write scopes. | Read-only enforcement (`agent.read`, `profile.read`). | Guardrail: agent access tokens are strictly prohibited from performing state-mutating writes (POST/PUT/DELETE) on fiction entities. |
| **Replay Protection** | In-memory or Redis caching of `jti`. | Dual-layer: Redis distributed cache with fallback to PostgreSQL durable replay table (`agent_assertion_replays`). | Resilient against Redis restarts and multi-instance container environments. |

---

## 2. Phone Verification Gap (Primary TODO)

### The Problem
The WorkOS identity assertion protocol supports phone-only authentication flows (where an agent authenticates on behalf of a user verified via SMS OTP or WhatsApp rather than an email address). In such assertions:
- `phone_number` and `phone_number_verified` are populated.
- `email` and `email_verified` may be completely absent or empty.

In IHeartFiction, all user identities across PostgreSQL, MongoDB, and Keycloak are keyed primarily by `userId` (GUID) and correlated to `email`. 

If an agent submits an assertion containing only a verified phone number:
1. `AgentAssertionValidator` currently rejects the assertion with `missing_verified_email` ("A verified email is required.").
2. Keycloak user lookups (`FindUserIdByEmailAsync`) and user provisioning (`CreateVerifiedUserAsync`) cannot proceed without a valid, unique email address.

### Future Implementation Plan (TODO)
When phone-only agent registration is introduced, the following architectural additions must be implemented:

1. **Keycloak Phone Attribute Mapping**:
   - Configure Keycloak user profile attributes to store `phoneNumber` and `phoneNumberVerified`.
   - Implement `FindUserIdByPhoneAsync(string phoneNumber, CancellationToken ct)` in `KeycloakAdminService`.

2. **Synthetic or Optional Email Handling**:
   - For phone-only users, establish a standard synthetic email convention (e.g., `{phone_e164}@phone.auth.iheartfiction.net` or nullable email in local DB schemas where supported).
   - Alternatively, require an interactive claim step (`PendingClaim`) where the user links their phone identity to an existing or new email-verified account via browser confirmation.

3. **Assertion Validator Support**:
   - Update `ValidatedAgentIdentity` to include `string? VerifiedPhone`.
   - Update `AgentAssertionValidator` to accept assertions satisfying either `email_verified == true` OR `phone_number_verified == true`.
   - Ensure claim codes / verification prompts accurately display either the email or masked phone number being bound.

4. **Security & SIM-Swap Mitigations**:
   - Unlike verified emails (which often have 2FA and strong domain security), phone numbers are vulnerable to SIM swapping and carrier reassignment. Require shorter token lifetimes or mandatory re-confirmation for phone-backed agent registrations.

---

## 3. Approved Trusted Identity Providers

The following external providers are approved to issue `ID-JAG` identity assertions for autonomous agents:

| Provider | Issuer URI | JWKS Endpoint | Supported Algorithms |
| :--- | :--- | :--- | :--- |
| **WorkOS** | `https://api.workos.com/` | `https://api.workos.com/sso/jwks` | `RS256`, `ES256` |
| **Google** | `https://accounts.google.com` | `https://www.googleapis.com/oauth2/v3/certs` | `RS256`, `ES256` |
| **Microsoft Entra ID** | `https://login.microsoftonline.com/common/v2.0` | `https://login.microsoftonline.com/common/discovery/v2.0/keys` | `RS256` |
| **GitHub** | `https://token.actions.githubusercontent.com` | `https://token.actions.githubusercontent.com/.well-known/jwks` | `RS256`, `ES256` |
| **Cloudflare** | `https://auth.cloudflare.com/` | `https://auth.cloudflare.com/.well-known/jwks.json` | `RS256`, `ES256` |

---

## 4. Modern Identity Package Standardization

IHeartFiction standardizes on modern Microsoft Identity Model libraries:
- **`Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler`**: Replaces legacy `JwtSecurityTokenHandler` for all token issuance, validation, and claim extraction. Provides zero-allocation parsing, strict RFC 9068 compliance, and high throughput.
- **`Microsoft.IdentityModel.Protocols.OpenIdConnect.ConfigurationManager<OpenIdConnectConfiguration>`**: Used for automatic, cached discovery and rotation of provider JWKS key sets.
