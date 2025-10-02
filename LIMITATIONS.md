# Known Limitations

This document outlines the known limitations and challenges in the IHFiction project.

## Deployment

### Docker Swarm Deployment Labels (RESOLVED)

The issue in the .NET Aspire SDK that caused deployment labels to be emitted under `additional_labels` instead of `labels` has been fixed upstream. If you are running an Aspire release that includes the fix (Aspire 9.5, 9.4.3 or later), you no longer need the `docker-compose.deploy.yml` override; the generator will emit proper `deploy.labels`.

If you're running an older Aspire version, keep the documented override workaround in your deployment pipeline until you upgrade.

**Reference:** Aspire PR that fixed the behaviour: https://github.com/dotnet/aspire/pull/11204

### Other Swarm schema edge-cases (upstream fixes pending)

Some schema typing issues remain upstream and may not yet be present in the stable CLI. In particular:

- `Parallelism` and `FailOnError` schema types were reported and fixed in the Aspire codebase (see https://github.com/dotnet/aspire/pull/11706) but those fixes may not yet be available in older stable releases. We continue to document these items and work around them in the codebase where necessary.

### Production Configuration in AppHost.cs

The `AppHost.cs` file contains several configurations that are specific to a production environment and may require modification for other environments. These configurations are primarily within the `if (builder.Environment.IsProduction())` block.

*   **External Docker Network:** The configuration adds an external Docker network named `t3_proxy`. This is likely specific to a particular deployment environment using a reverse proxy.

*   **Hardcoded Secret File Paths:** Docker secrets are defined with hardcoded relative paths (e.g., `./secrets/keycloak-admin-pass.secret`). These paths will need to be adjusted for different environments.

*   **Docker Secrets for Passwords:** For services like PostgreSQL, MongoDB, and Keycloak, the password environment variables are removed and replaced with `*_FILE` variables to use Docker secrets. This is a security best practice but requires the secrets to be set up correctly in the deployment environment.

*   **Manual Replica Count for Swarm:** The replica count for services is set manually. This is a workaround for a bug where the `ReplicaAnnotation` is not correctly translated to the `deploy.replicas` field in the generated `docker-compose.yml` for Docker Swarm.

*   **HTTP-Only Endpoints:** The HTTPS endpoints for the API and web client are removed, and the HTTP endpoints are set to port 8080. This is done to work with an external reverse proxy that handles TLS termination.

*   **Container Image Registry:** The container image names are prefixed with a container registry specified in the configuration. This needs to be configured for the target deployment environment.
