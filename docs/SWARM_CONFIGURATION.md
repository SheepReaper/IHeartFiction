# Swarm configuration publication

Swarm deployment artifacts must not depend on Compose `.env` interpolation.
Required non-secret settings are resolved when the AppHost publishes the model;
credentials are supplied through mounted Swarm secrets. Provision secret sources
through the private deployment configuration before deployment. Do not put secret
values, source paths, or environment-specific infrastructure details in this file.

## Application secrets

The .NET services use the configured `SecretsPath`, defaulting to `/run/secrets`
when omitted. Overrides must match the secret mount targets; changing the setting
does not relocate mounted files. Service defaults load that directory with the
key-per-file configuration provider. Double underscores in
secret filenames become configuration separators, while hyphens in connection
names are preserved.

| Services | Required mounted connection secrets |
|---|---|
| Migration service | `ConnectionStrings__fiction-db`, `ConnectionStrings__stories-db` |
| FictionApi | Those two connections and `ConnectionStrings__redis` |
| WebClient | `ConnectionStrings__fiction-db` |

These are complete connection strings. Aspire-generated environment aliases
using either hyphens or underscores, and derived host/port/username/URI/JDBC
properties, are unnecessary for these consumers and are removed from production
service environments. Internal HTTP service-discovery settings remain.

FictionApi also requires `AgentAuth__SigningKeyPem` for agent token signing. The
existing signing key remains a mounted secret; publication must not generate,
replace, or rotate it. Signing-key and VAPID parameters are created only outside
production. OIDC credentials, VAPID keys, and telemetry credentials retain their
existing service-specific secret mounts.

Database image initialization uses the supported password-file environment
settings. Redis reads its password file in the container entrypoint and health
check. Keycloak, the tunnel connector, and the dashboard retain their existing
secret-file configuration mechanisms.

## Publication checks

Publish from the AppHost, then validate with the Swarm parser:

```powershell
aspire publish --apphost src/aspire/IHFiction.AppHost -o infra --non-interactive
docker stack config --compose-file infra/docker-compose.yaml > $null
```

If using a separate deployment override, include it in the parser check. Keep the
rendered configuration private. The parser check does not deploy services or
prove that production secret files exist or contain valid credentials.

Production publication rejects environment values that still require host-side
variable substitution. Generated `.env` entries are cleared. Use a clean output
directory when checking publication: Aspire does not remove an obsolete `.env`
left by an earlier publication. Remove that stale generated file after confirming
it is not a manually maintained configuration source.

Escaped `$$(cat /run/secrets/...)` expressions in Redis commands are intentional.
They become shell substitutions inside the container, reading the mounted secret;
they are not deployment-time `.env` substitutions.

Regression tests cover both connection-name aliases, preservation of service
discovery, rejection of host interpolation, and loading complete connection
strings and a multiline PEM key through the real key-per-file provider.
