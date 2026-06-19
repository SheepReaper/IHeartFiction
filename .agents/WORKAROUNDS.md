# Temporary Workarounds

This file records intentional temporary workarounds and unusual build plumbing. Check it before removing code that looks like a local band aid.

## WebClient publish filters FictionApi appsettings

- **Location:** `src/IHFiction.WebClient/IHFiction.WebClient.csproj`, target `RemoveFictionApiConfigurationFromPublishOutput`
- **Symptom:** `aspire do build-web -o infra -v` or `aspire do push -o infra -v` fails in the `build-web` step with `NETSDK1152`, listing duplicate `appsettings*.json` files from `IHFiction.FictionApi` and `IHFiction.WebClient`.
- **Why it is needed:** `src/lib/IHFiction.SharedWeb/IHFiction.SharedWeb.csproj` has a `ProjectReference` to `IHFiction.FictionApi` with `ReferenceOutputAssembly="false"`. That reference is intentional: it keeps `FictionApi` in the MSBuild graph so `openapi.json` is generated before the SharedWeb source generator consumes it. Because `FictionApi` is an ASP.NET Core Web SDK project, the .NET SDK publish pipeline still sees its `appsettings*.json` files through the referenced project graph. When Aspire builds the `web` container image, it invokes `dotnet publish` for `IHFiction.WebClient`; the SDK then detects that both projects would publish files with the same relative paths and raises `NETSDK1152`.
- **Is this an Aspire bug?** Not currently. Aspire is the caller that exposes the problem during container-image publishing, but the duplicate-file error is a .NET SDK publish behavior. Microsoft documents this as SDK behavior introduced in .NET 6: duplicate publish-relative files from project references now fail instead of allowing arbitrary overwrite. See `NETSDK1152` and "Generate error for duplicate files in publish output" in the .NET compatibility docs: https://learn.microsoft.com/dotnet/core/compatibility/sdk/6.0/duplicate-files-in-output
- **Why it was not needed before:** The repo previously generated the API client through a separate generator project/target. Commit `6b8f404` moved generation to `IHFiction.SourceGenerators` and added the `SharedWeb` -> `FictionApi` build-order project reference. That made `FictionApi` visible in WebClient's publish graph.
- **Why the workaround is narrow:** The target removes only `ResolvedFileToPublish` items whose source identity is from `IHFiction.FictionApi`, whose filename starts with `appsettings`, and whose extension is `.json`. It does not remove the project reference, does not disable `NETSDK1152`, and does not suppress WebClient's own configuration files.
- **Removal criteria:** Remove this target only after one of these is true:
  - `SharedWeb` no longer needs a `ProjectReference` to the Web SDK `FictionApi` project for OpenAPI/source-generator build ordering.
  - The OpenAPI input is produced by a non-Web project or explicit build artifact that does not enter WebClient's publish graph.
  - A future .NET SDK or Aspire release provides project-reference publish metadata that excludes referenced web-project content while preserving MSBuild graph ordering, and `aspire do build-web -o infra -v --non-interactive` passes without the target.

When testing removal, verify both ordinary builds and the Aspire container build step:

```powershell
./tools/agent-bootstrap.ps1
dotnet build .\src\IHFiction.WebClient\ --no-restore
dotnet build .\src\aspire\IHFiction.AppHost\IHFiction.AppHost.csproj --no-restore
aspire do build-web -o infra -v --non-interactive
```

## Local source-generator package feed and deterministic repack guard

- **Location:** `tools/agent-bootstrap.ps1`, `tools/agent-bootstrap.sh`, `src/lib/IHFiction.SourceGenerators/IHFiction.SourceGenerators.csproj`, `.artifacts/packages/IHFiction.SourceGenerators.0.1.0-local.nupkg`
- **Symptom:** Consumers of `IHFiction.SourceGenerators` need the latest local analyzer/source-generator code before normal repo restore/build. A plain project reference is not enough because analyzers are consumed through NuGet analyzer assets.
- **Why it is needed:** The repo packages `IHFiction.SourceGenerators` as version `0.1.0-local` into a local feed under `.artifacts/packages`, then restores projects that reference that package. The bootstrap scripts also clear the global NuGet cache for that local version so restore does not reuse a stale analyzer package.
- **Unusual plumbing:** NuGet `.nupkg` output is not byte-for-byte deterministic because generated package metadata entries such as `_rels/.rels` and `package/services/metadata/core-properties/*.psmdcp` change between packs. The bootstrap scripts therefore pack into a temp feed, compare normalized package payload entries while ignoring those metadata entries, and only replace the tracked local package when real content changed.
- **Why the workaround is narrow:** The normalized comparison ignores only NuGet-generated package metadata. Analyzer DLLs and packaged dependency payloads still participate in the content hash comparison.
- **Removal criteria:** Remove this local package/feed flow only after source generators are consumed in a way that does not require a local NuGet package, or after `IHFiction.SourceGenerators` is published to a real package feed with a versioning workflow that replaces `0.1.0-local`.

When testing removal or changes:

```powershell
./tools/agent-bootstrap.ps1
git status --short
dotnet build .\src\IHFiction.WebClient\ --no-restore
```

Running bootstrap twice without source-generator changes should not dirty `.artifacts/packages/IHFiction.SourceGenerators.0.1.0-local.nupkg`.

## SharedWeb OpenAPI source-generator build edge

- **Location:** `src/lib/IHFiction.SharedWeb/IHFiction.SharedWeb.csproj`
- **Symptom:** `IHFiction.SharedWeb` needs `src/IHFiction.FictionApi/openapi.json` available before its source generator runs.
- **Why it is needed:** `SharedWeb` includes `openapi.json` as an `AdditionalFiles` input for `IHFiction.SourceGenerators`. The `ProjectReference` to `IHFiction.FictionApi` uses `ReferenceOutputAssembly="false"` so SharedWeb does not compile against the API assembly, but the project remains in the MSBuild graph so normal graph scheduling builds the API and regenerates OpenAPI before SharedWeb compilation.
- **Important constraint:** Do not replace this with a manual `MSBuild` target that builds `FictionApi`; that can create parallel builds of the same project and cause Windows file-lock failures in `obj/bin`. If this reference is changed, verify both ordinary builds and the WebClient publish workaround above.
- **Removal criteria:** Remove only when OpenAPI input generation no longer requires building the Web SDK API project as part of SharedWeb's graph, for example if OpenAPI is generated by a separate non-Web contract project or checked-in artifact with a separate freshness check.

## Aspire Docker/Swarm publish customization uses preview APIs

- **Location:** `src/aspire/IHFiction.AppHost/AppHost.cs`, `src/aspire/IHFiction.AppHost/Extensions/ProductionConfigExtensions.cs`, `src/aspire/IHFiction.AppHost/Extensions/DockerComposeExtensions.cs`, `src/aspire/IHFiction.AppHost/Extensions/WellKnownTests.cs`, `src/aspire/IHFiction.AppHost/IHFiction.AppHost.csproj`
- **Symptom:** Production deployment needs Docker Compose/Swarm output that Aspire's stable abstractions do not fully model yet, including OCI multi-platform image output, Docker health checks, Swarm deployment shape, Traefik labels, Docker secrets, and Cloudflare tunnel service customization.
- **Why it is needed:** The AppHost relies on Aspire APIs that are marked evaluation/preview, so warnings such as `ASPIREPIPELINES003`, `ASPIRECOMPUTE003`, `ASPIREDOCKERFILEBUILDER001`, `ASPIREPROBES001`, and `ASPIRE004` are explicitly suppressed. The production extensions then post-process generated Docker Compose resources for Swarm-specific behavior.
- **Related upstream limitations:** `LIMITATIONS.md` tracks several deployment limitations and upstream fixes, including Docker Swarm label behavior and schema typing issues around `Parallelism` and `FailOnError`.
- **Removal criteria:** Revisit on every Aspire major/minor upgrade. Remove suppressions and custom publish mutations when Aspire exposes stable APIs that produce the required Docker Compose/Swarm output directly.

Verification should include:

```powershell
./tools/agent-bootstrap.ps1
dotnet build .\src\aspire\IHFiction.AppHost\IHFiction.AppHost.csproj --no-restore
aspire do build-web -o infra -v --non-interactive
```

Use `aspire do push -o infra -v` only when intentionally pushing images.

## Docker Swarm Compose schema cleanup and manual deploy fields

- **Location:** `src/aspire/IHFiction.AppHost/Extensions/ProductionConfigExtensions.cs`, `src/aspire/IHFiction.AppHost/Extensions/DockerSwarmExtensions.cs`, `LIMITATIONS.md`
- **Symptom:** Aspire's Docker Compose output is close to what Swarm needs but includes fields Swarm ignores or parses differently, and some deployment fields are not generated from Aspire annotations.
- **Why it is needed:** The production Compose customization clears `depends_on`, `expose`, and container `restart` values that are not useful for Swarm. It also manually sets `deploy.replicas` from Aspire replica annotations because those annotations are not emitted as Swarm replicas in the generated Compose output. `DockerSwarmExtensions.AddGracefulUpdate` avoids fields whose generated schema typing has had upstream issues.
- **Removal criteria:** Remove the cleanup/manual assignment only after generated Aspire Docker Compose output can be deployed to Swarm without those edits and `LIMITATIONS.md` confirms the upstream fixes are present in the repo's pinned Aspire version.

## Temporary transitive vulnerability package overrides

- **Location:** `src/Directory.Build.props`
- **Symptom:** Transitive dependencies from tooling/analyzers have had vulnerability advisories, and central package overrides are used to force safer versions.
- **Why it is needed:** The source-level build props currently add direct package references for `SharpCompress`, `Snappier`, and `StreamJsonRpc` with comments pointing to the relevant GitHub security advisories. These references are not domain dependencies; they are temporary dependency-resolution overrides.
- **Removal criteria:** Remove each direct reference when the packages that originally brought in the vulnerable transitive dependencies have upgraded far enough that `dotnet list package --vulnerable --include-transitive` stays clean without the override.

Suggested check:

```powershell
dotnet list package --vulnerable --include-transitive
dotnet build .\src\IHFiction.WebClient\ --no-restore
```

## Microsoft.OpenApi 2.x version range pin

- **Location:** `Directory.Packages.props`
- **Symptom:** OpenAPI generation/source-generation depends on the .NET 10 OpenAPI stack, which expects Microsoft.OpenApi 2.x APIs.
- **Why it is needed:** `Microsoft.OpenApi` is pinned to `[2.7.5,3.0.0)` so dependency resolution does not float to 3.x and break the generator/tooling API surface.
- **Removal criteria:** Remove or widen this range only after the .NET OpenAPI generator and `IHFiction.SourceGenerators` support the newer Microsoft.OpenApi major version.

Verification:

```powershell
./tools/agent-bootstrap.ps1
dotnet build .\src\IHFiction.WebClient\ --no-restore
```

## FictionApi OpenAPI generation disabled for cross-runtime and CI builds

- **Location:** `src/IHFiction.FictionApi/IHFiction.FictionApi.csproj`
- **Symptom:** OpenAPI document generation runs the built API assembly via `Microsoft.Extensions.ApiDescription.Server`. This is useful for local/default builds but fragile or wasteful during container cross-runtime builds and CI builds.
- **Why it is needed:** The project disables `OpenApiGenerateDocuments` when `RuntimeIdentifier` or `ContainerRuntimeIdentifier` differs from `HostRuntimeIdentifier`, and when `ContinuousIntegrationBuild=true`. This avoids trying to execute an assembly for a runtime that may not match the current host and keeps CI deterministic.
- **Tradeoff:** Builds where OpenAPI generation is disabled rely on the existing `src/IHFiction.FictionApi/openapi.json` artifact. If API contract changes are made, run a normal host-runtime build locally to refresh it before relying on SharedWeb source generation.
- **Removal criteria:** Remove only if OpenAPI generation becomes host-independent for cross-runtime publish/container builds, or if the API client generation no longer depends on a generated OpenAPI file.

## Blazor Server editor SignalR payload size increase

- **Location:** `src/IHFiction.WebClient/Program.cs`
- **Symptom:** Large paste operations in the Markdown/content editor can exceed the default Blazor Server SignalR message size.
- **Why it is needed:** The app raises `MaximumReceiveMessageSize` to 10 MB so large editor payloads do not immediately fail.
- **Tradeoff:** This increases the maximum server-side message size globally for interactive server components.
- **Removal criteria:** Replace with client-side chunking or another upload/edit protocol that does not send very large editor payloads as single SignalR messages. After removal, manually test large paste/edit flows in the story/chapter editor.

## Universal HATEOAS instead of content negotiation

- **Location:** API response design; see `LIMITATIONS.md` under ".NET 10 Minimal API Content Negotiation"
- **Symptom:** Minimal APIs do not provide the content negotiation behavior needed to automatically switch between plain JSON and HATEOAS-enhanced JSON based on `Accept` headers.
- **Why it is needed:** Endpoint `.Produces<T>()` metadata is documentation-oriented and does not provide runtime response selection. Rather than hand-rolling negotiation on every endpoint, the project currently returns hypermedia links universally.
- **Tradeoff:** Clients always receive link metadata, even when they would otherwise prefer a smaller plain representation.
- **Removal criteria:** Revisit if ASP.NET Core Minimal APIs gain first-class response content negotiation and OpenAPI support for multiple response schemas/content types per endpoint, or if the project adopts a separate endpoint/versioning strategy for hypermedia.

## MongoDB EF Core package retained mostly for ObjectId serialization

- **Location:** data access code and `Directory.Packages.props`; see `LIMITATIONS.md` under "MongoDB.EntityFrameworkCore Package Compatibility Issues"
- **Symptom:** `MongoDB.EntityFrameworkCore` has EF Core 10 compatibility gaps, including incomplete discriminator behavior and missing EF-style async query methods.
- **Why it is needed:** The project bypasses the EF abstraction for MongoDB operations and uses the MongoDB driver directly where needed. The `MongoDB.EntityFrameworkCore` package remains installed primarily for `ObjectIdJsonConverter`, which keeps `ObjectId` serialization consistent when MongoDB identifiers flow through PostgreSQL-backed entities and JSON APIs.
- **Tradeoff:** Data access patterns differ between PostgreSQL and MongoDB, and the dependency remains even though most MongoDB data access avoids EF Core.
- **Removal criteria:** Remove when either MongoDB.EntityFrameworkCore fully supports the repo's EF Core version and desired async/query behavior, or the project replaces the needed converter with an internal `ObjectIdJsonConverter` and can drop the package.
