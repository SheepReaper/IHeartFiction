# I❤️Fiction

[![build](https://github.com/SheepReaper/IHeartFiction/actions/workflows/build.yml/badge.svg)](https://github.com/SheepReaper/IHeartFiction/actions/workflows/build.yml) <a href="https://stats.uptimerobot.com/1uMnPit0yS?utm_source=status_badge&utm_medium=referral" target="_blank" rel="noopener"><picture><source media="(prefers-color-scheme: dark)" srcset="https://badge.uptimerobot.com/psp/099ef2c7c2afdc0f08b72a8492d3f42b.svg?style=logo&theme=dark"><img src="https://badge.uptimerobot.com/psp/099ef2c7c2afdc0f08b72a8492d3f42b.svg?style=logo&theme=light" alt="I❤️Fiction"></picture></a>

I❤️Fiction is an ambitious open-source project to build a modern, feature-rich platform for both original and fan fiction. Built on a .NET and ASP.NET Core backend with a Blazor frontend, it aims to be a viable competitor to established platforms by focusing on a clean user experience, powerful authoring tools, and a strong community.

## Getting Started

To get the project running locally, you'll need the following installed:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- `python3` on Linux when forcing a local source-generator package refresh

Restore the repository-local Aspire CLI and `dotnet-ef`, restore dependencies, and validate the environment:

```bash
./tools/agent-bootstrap.sh
```

On Windows PowerShell:

```powershell
./tools/agent-bootstrap.ps1
```

Then start the AppHost in the background:

```bash
aspire start
```

Use `dotnet run --project src/aspire/IHFiction.AppHost` when a foreground process is preferable.

### After the stack starts (First-time Set-up)

The Keycloak realm `fiction` is pre-configured by the realm import file. However the client secrets are not. 

1. Explore the Keycloak resource properties in the Aspire Dashboard. You should find the admin user and password for the Keycloak server.
1. Once the Keycloak service status shows healthy in the Aspire Dashboard, click the link to the Keycloak server. Once in, access the fiction realm and its clients.
1. Regenerate and copy the credential (secret) for `fiction-admin-client`.
1. The first time the Aspire Dashboard launches for you, you should be prompted to provide missing secrets. You can click this message to provide the secret you just generated.
1. (Alternatively) Set the admin-client secret with `dotnet user-secrets --project ./src/aspire/IHFiction.AppHost set Parameters:ApiKeycloakAdminClientSecret <YOUR_SECRET_HERE>`.
1. Repeat the last two steps for `fiction-frontend`, using `Parameters:ApiOidcClientSecret`.

### Agent authentication signing key

Local Aspire runs generate and persist an ECDSA P-256 agent signing key as an AppHost secret. Before deploying the production Compose stack, generate the corresponding Docker secret file on the cluster:

```powershell
./tools/New-AgentAuthSigningKey.ps1 -OutputPath /mnt/swarm/config/ihfiction/secrets/agent-auth-signing-key.secret
```

The generated Compose file mounts that file read-only as `AgentAuth__SigningKeyPem`. Keep the private PEM out of source control and back it up. During rotation, move the old public PEM and key ID into `AgentAuth:PreviousVerificationKeys` until every token it signed has expired; JWKS and token validation publish and accept those public keys without retaining old private keys. Configure each trusted provider under `AgentAuth:TrustedProviders` with an exact issuer, explicit JWKS URL, allowed client IDs, and signing algorithms; the default empty list intentionally trusts no provider.

## Software Stack

The project is built on the .NET platform, embracing a modern, cloud-native architecture.

- **Backend:** ASP.NET Core
- **Framework:** ASP.NET Core using Minimal APIs for a lightweight and high-performance service layer.
- **Frontend:** Blazor Web App for a rich, interactive user experience.
- **Orchestration:** .NET Aspire to manage and compose the various services that make up the application.
- **Database:** PostgreSQL for robust and scalable data storage.
- **Data Access:** Entity Framework Core (EFCore) for object-relational mapping.
- **Async Workflows & Messaging:** WolverineFx for asynchronous command/event processing and service-domain messaging.
- **Authentication:** Keycloak for secure and flexible identity and access management.
- **Containerization:** Docker and Docker Compose for consistent development and deployment environments.

## Architecture

For a detailed explanation of the project's architecture, design philosophy, and technical decisions, please see [ARCHITECTURE.md](ARCHITECTURE.md).

## Support Us

I❤️Fiction is a community-driven project. If you'd like to support our work, please consider sponsoring us.
