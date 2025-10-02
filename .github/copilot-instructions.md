# Copilot Instructions for IHeartFiction

## Repository Overview

**IHeartFiction** is a modern, cloud-native fiction reading and publishing platform built on .NET 10 (pre-release) using .NET Aspire for orchestration. The platform supports both original and fan fiction with a focus on clean UX, powerful authoring tools, and strong community features.

**Key Statistics:**
- ~8MB repository size
- 234 C# source files, 277 total project files
- 39 test files across unit and integration tests
- Target Framework: .NET 10.0 (net10.0)
- Architecture: Vertical Slice Architecture with CQRS Lite pattern

## Critical Prerequisites

### Required Software

**ALWAYS verify these are installed before attempting any build operations:**

1. **.NET 10 SDK RC (10.0.100-rc.1 or later)** - This is MANDATORY. The project uses .NET 10 RC features.
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
   - Check version: `dotnet --version`
   - The project WILL NOT build with .NET 8 or .NET 9

2. **Docker Desktop** - Required for running the application stack (PostgreSQL, MongoDB, Keycloak)
   - Download: https://www.docker.com/products/docker-desktop/

3. **(Optional) Aspire CLI** - Simplifies running the application
   - Install: `dotnet workload install aspire`

### Verification Steps

Before making any code changes, ALWAYS run:
```bash
dotnet --version  # Must show 10.0.100-rc.1 or later
docker --version  # Confirm Docker is installed
```

If .NET 10 SDK is not installed, STOP and inform the user. Do not attempt to build with older SDKs.

## Build and Test Commands

### Project Structure

The solution uses **Central Package Management** via `Directory.Packages.props` in the repository root. All package versions are centrally managed.

### Build Process

**IMPORTANT:** Always build in Debug configuration first to match CI/CD pipeline:

```bash
# Step 1: Restore dependencies (always run first)
dotnet restore

# Step 2: Build in Debug configuration (matches CI workflow)
dotnet build --configuration Debug --no-restore

# Step 3: Run tests (if code changes were made)
dotnet test -c Release --logger "GitHubActions;report-warnings=false"
```

**Build Warnings:**
- The build has `TreatWarningsAsErrors=true` for all configurations
- Code analysis is enabled with `AnalysisMode=All`
- SonarAnalyzer is enabled for all projects
- Analysis is DISABLED for Release builds (`RunAnalyzers=false` in Release)

### Running Tests

The project uses **xUnit v3** with the following test frameworks:
- FluentAssertions (pinned to v7.x due to licensing)
- NSubstitute for mocking
- Testcontainers for integration tests (PostgreSQL, MongoDB)
- GitHub Actions Test Logger for CI

**Run all tests:**
```bash
dotnet test -c Release --logger "GitHubActions;report-warnings=false"
```

**Integration tests** use Testcontainers and require Docker to be running. If Docker is not available, integration tests will fail.

## Running the Application

### Development Mode

**Method 1: Using .NET CLI (Recommended)**
```bash
dotnet run --project src/aspire/IHFiction.AppHost
```

**Method 2: Using Aspire CLI (if installed)**
```bash
aspire run
```

### First-Time Setup (CRITICAL)

After the first run, you MUST configure Keycloak secrets:

1. **Access Aspire Dashboard** - Opens automatically after `dotnet run`
2. **Get Keycloak admin credentials** - Found in Keycloak resource properties in Dashboard
3. **Wait for Keycloak to be healthy** - Check status in Dashboard (can take 1-2 minutes)
4. **Access Keycloak admin console** - Click the Keycloak link in Dashboard
5. **Navigate to fiction realm** → Clients
6. **Regenerate secrets for:**
   - `fiction-admin-client`
   - `fiction-frontend`
7. **Set secrets using dotnet CLI:**
   ```bash
   dotnet user-secrets --project ./src/aspire/IHFiction.AppHost/ set Parameters:KeycloakAdminClientSecret <SECRET_1>
   dotnet user-secrets --project ./src/aspire/IHFiction.AppHost/ set Parameters:ApiOidcClientSecret <SECRET_2>
   ```

**Note:** The Aspire Dashboard will prompt for missing secrets on subsequent runs.

## Project Architecture

### Architectural Patterns

1. **Vertical Slice Architecture** - Features organized by business capability, not technical layers
2. **CQRS Lite** - Separation of Commands (writes) and Queries (reads)
3. **Shared Kernel** - Common code in `IHFiction.SharedKernel`
4. **Primary Keys: ULIDs** - Lexicographically sortable, URL-safe identifiers

### Solution Structure

```
/
├── src/
│   ├── IHFiction.FictionApi/          # ASP.NET Core Minimal APIs backend
│   ├── IHFiction.WebClient/           # Blazor Web App frontend
│   ├── aspire/
│   │   ├── IHFiction.AppHost/         # .NET Aspire orchestration (ENTRYPOINT)
│   │   └── IHFiction.MigrationService/ # EF Core migrations runner
│   └── lib/
│       ├── IHFiction.Data/            # EF Core entities, DbContexts, migrations
│       ├── IHFiction.ServiceDefaults/ # Shared service configuration
│       ├── IHFiction.SharedKernel/    # Common validation, domain logic
│       └── IHFiction.SharedWeb/       # Shared Blazor components
├── tests/
│   ├── IHFiction.UnitTests/           # xUnit unit tests
│   └── IHFiction.IntegrationTests/    # Integration tests with Testcontainers
├── config/
│   └── fiction-realm.json             # Keycloak realm configuration
└── .github/workflows/                 # CI/CD pipelines
```

### Key Project Files

- **Entrypoint:** `src/aspire/IHFiction.AppHost/AppHost.cs` - Defines the application stack
- **API:** `src/IHFiction.FictionApi/Program.cs` - Minimal API endpoints
- **Web:** `src/IHFiction.WebClient/Program.cs` - Blazor app configuration
- **Data:** `src/lib/IHFiction.Data/Contexts/FictionDbContext.cs` - PostgreSQL context
- **Data:** `src/lib/IHFiction.Data/Contexts/StoryDbContext.cs` - MongoDB context

### Database Migrations

EF Core migrations are in `src/lib/IHFiction.Data/Migrations/`. The MigrationService applies them automatically in non-development environments.

**To add a new migration (PostgreSQL):**
```bash
dotnet ef migrations add <MigrationName> --project src/lib/IHFiction.Data/IHFiction.Data.csproj --context FictionDbContext
```

## Configuration Files

### Code Style and Analysis

- **`.editorconfig`** - C# code style rules (4-space indent, file-scoped namespaces, etc.)
- **`Directory.Packages.props`** - Central package version management
- **`global.json`** - SDK version pinned to 10.0.100-rc.1

### Application Settings

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- User secrets for sensitive data (Keycloak secrets)

## CI/CD Workflows

### Build Workflow (`.github/workflows/build.yml`)

**Triggers:** Push to main, tags (v*), pull requests

**Steps:**
1. Checkout code
2. Setup .NET SDK (uses global.json)
3. **Build:** `dotnet build --configuration Debug` (NOT Release!)
4. **Test:** `dotnet test -c Release --logger "GitHubActions;report-warnings=false"`
5. Analyze tags for release detection

**IMPORTANT:** CI builds in Debug but tests in Release.

### CodeQL Workflow (`.github/workflows/codeql.yml`)

**Triggers:** Push to main, pull requests, weekly schedule

**Steps:**
1. Initialize CodeQL for C# and JavaScript/TypeScript
2. **Restore:** `dotnet restore`
3. **Build:** `dotnet build --no-restore --configuration Release`
4. Perform security analysis

**Note:** CodeQL builds in Release configuration.

## Known Limitations and Workarounds

### 1. .NET Aspire Docker Swarm Bug

**Problem:** Aspire incorrectly generates `additional_labels` instead of `labels` in swarm deployment sections.

**Workaround:** See `LIMITATIONS.md` for docker-compose override approach.

**Status:** Fixed in Aspire 9.5/9.4.3 (PR #11204 merged)

**Notes:** The long-form workaround previously documented in `LIMITATIONS.md` has been removed from the repo (see commit history) now that the upstream fix is merged. If you are using an older Aspire CLI, the override documented in `LIMITATIONS.md` may still be required.

**Related upstream work:** Other Swarm-schema issues (for example, `Parallelism` and `FailOnError` typing mismatches) were addressed in a follow-up Aspire PR (see https://github.com/dotnet/aspire/pull/11706). Those fixes may not have propagated to all Aspire releases yet; check your Aspire CLI version if you hit related issues.

### 2. Production Configuration

Production deployment uses Docker Swarm with:
- External network `t3_proxy` (environment-specific)
- Docker secrets from `./secrets/` directory (paths are hardcoded)
- Manual replica count configuration (workaround for Aspire bug)
- HTTP-only endpoints (TLS termination at reverse proxy)

See `src/aspire/IHFiction.AppHost/Extensions/ProductionConfigExtensions.cs` for full configuration.

### 3. Known BUG Comments in Code

Several `BUG:` comments exist in `ProductionConfigExtensions.cs`:
- Swarm parser incompatibilities with depends_on long-format
- Replica count not auto-set from ReplicaAnnotation
- Parallelism and FailOnError type mismatches in schema

These are tracked Aspire SDK issues with documented workarounds in place.

## Development Guidelines

### API Development

The FictionApi uses **ASP.NET Core Minimal APIs** organized by feature slices:
- `src/IHFiction.FictionApi/Authors/` - Author management endpoints
- `src/IHFiction.FictionApi/Stories/` - Story CRUD operations
- `src/IHFiction.FictionApi/Tags/` - Tagging system

**Endpoint Pattern:**
```csharp
internal sealed class CreateStoryRequest { ... }
internal sealed class CreateStory { ... }  // Use case handler
```

**Built-in Validation:** .NET 10 automatically validates `DataAnnotation` attributes on request models.

**Query Parameters:** Use interfaces from `IHFiction.SharedKernel/Validation/` for consistent parameter patterns:
- `IPaginationSupport` - Page, PageSize
- `ISortingSupport` - SortBy, SortOrder
- `ISearchSupport` - Search query
- `IFilterSupport` - Filtering

### Frontend Development

Blazor Web App in **Interactive Server mode** with typed HTTP client generated from OpenAPI schema at build time.

**Key Components:** `src/lib/IHFiction.SharedWeb/`

### Testing Patterns

**Unit Tests:** Service-based approach without DbContext mocking (see `tests/IHFiction.UnitTests/Authors/GetAuthorByIdServiceTests.cs`)

**Integration Tests:** Use Testcontainers for real database instances.

### Validation Attributes

Custom validation attributes in `src/lib/IHFiction.SharedKernel/Validation/`:
- `ValidMarkdownAttribute` - Validates markdown content security
- `NoHarmfulContentAttribute` - XSS/script injection protection
- `NoExcessiveWhitespaceAttribute` - Prevents whitespace abuse

## Common Pitfalls

1. **Building without .NET 10 SDK** - Will fail with NETSDK1045 error
2. **Not configuring Keycloak secrets** - App will not start properly after first run
3. **Docker not running** - Integration tests and app stack will fail
4. **Building in wrong configuration** - CI builds Debug first, then tests Release
5. **Missing migrations** - MigrationService must complete before API starts
6. **Treating warnings as errors** - All warnings must be resolved for successful build

## Quick Reference

**Start development:**
```bash
dotnet run --project src/aspire/IHFiction.AppHost
```

**Run tests:**
```bash
dotnet test -c Release
```

**Check for issues:**
```bash
dotnet build --configuration Debug  # Matches CI
```

**Add migration:**
```bash
dotnet ef migrations add <Name> --project src/lib/IHFiction.Data/IHFiction.Data.csproj --context FictionDbContext
```

## Trust These Instructions

These instructions have been carefully validated. Only search for additional information if:
1. These instructions are incomplete for your specific task
2. You encounter errors not documented here
3. Prerequisites or commands have been updated since this was written

For architecture details, see `ARCHITECTURE.md`. For deployment limitations, see `LIMITATIONS.md`.
