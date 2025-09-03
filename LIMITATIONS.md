# Known Limitations

This document outlines the known limitations and challenges in the IHFiction project.

## Deployment

### Docker Swarm Deployment Labels

A bug exists in the .NET Aspire SDK that prevents the correct generation of deployment labels for Docker Swarm services. When attempting to add labels to the `deploy` section of a service, Aspire incorrectly serializes them under an `additional_labels` key instead of the correct `labels` key.

This is caused by a bug in the `Aspire.Hosting.Docker.Resources.ServiceNodes.Swarm.LabelSpecs` class, which has an incorrect `YamlMember` attribute. This issue has been reported to the .NET Aspire team. ([#11197](https://github.com/dotnet/aspire/issues/11197))

The incorrect output looks like this:
```yaml
services:
  myservice:
    deploy:
      additional_labels: # This is incorrect
        foo: bar
```

**Workaround:**

The workaround I'm using is to not define the deployment labels in the `AppHost` project. Instead, create a separate Docker Compose override file (e.g., `docker-compose.deploy.yml`) and specify the deployment labels there.

**1. Override File (`docker-compose.deploy.yml`):**
```yaml
# docker-compose.deploy.yml
services:
  # Service name must match the name in the generated compose file
  postgres:
    deploy:
      labels:
        com.docker.compose.service: postgres
```

**2. Deployment Command:**
Use both the Aspire-generated compose file and the override file when deploying the stack. The override file should be specified last.
```shell
docker stack deploy -c docker-compose.yml -c docker-compose.deploy.yml my_stack
```

This approach uses a standard Docker Compose feature to merge the configurations, resulting in a valid deployment manifest without generating incorrect files.

**Update:**
PR is out for the fix: [#11204](https://github.com/dotnet/aspire/pull/11204)
PR Is merged and should be included in 9.5 or 9.4.3 version

### Production Configuration in AppHost.cs

The `AppHost.cs` file contains several configurations that are specific to a production environment and may require modification for other environments. These configurations are primarily within the `if (builder.Environment.IsProduction())` block.

*   **External Docker Network:** The configuration adds an external Docker network named `t3_proxy`. This is likely specific to a particular deployment environment using a reverse proxy.

*   **Hardcoded Secret File Paths:** Docker secrets are defined with hardcoded relative paths (e.g., `./secrets/keycloak-admin-pass.secret`). These paths will need to be adjusted for different environments.

*   **Docker Secrets for Passwords:** For services like PostgreSQL, MongoDB, and Keycloak, the password environment variables are removed and replaced with `*_FILE` variables to use Docker secrets. This is a security best practice but requires the secrets to be set up correctly in the deployment environment.

*   **Manual Replica Count for Swarm:** The replica count for services is set manually. This is a workaround for a bug where the `ReplicaAnnotation` is not correctly translated to the `deploy.replicas` field in the generated `docker-compose.yml` for Docker Swarm.

*   **HTTP-Only Endpoints:** The HTTPS endpoints for the API and web client are removed, and the HTTP endpoints are set to port 8080. This is done to work with an external reverse proxy that handles TLS termination.

*   **Container Image Registry:** The container image names are prefixed with a container registry specified in the configuration. This needs to be configured for the target deployment environment.
