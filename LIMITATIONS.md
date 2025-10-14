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

## API Framework Limitations

### .NET 10 Minimal API Content Negotiation

.NET 10's Minimal APIs have significant limitations regarding content negotiation that directly impact HATEOAS (Hypermedia as the Engine of Application State) implementations and custom media type support.

#### Core Design Philosophy and Limitation

Minimal APIs were designed with a "convention over configuration" philosophy that assumes JSON-first responses. The framework **lacks built-in content negotiation** - a fundamental limitation that prevents automatic response format selection based on client `Accept` headers.

**Key Issues:**
- No automatic `Accept` header parsing for response format selection
- Response serialization is hardcoded to use the configured `JsonSerializerOptions`
- Cannot conditionally return different response models based on requested content type

#### `.Produces<T>()` and `.Accepts<T>()` Behavior

The `.Produces<T>()` and `.Accepts<T>()` methods are **documentation-only** and do not enable runtime content negotiation:

- `.Produces<T>()` generates OpenAPI metadata but doesn't affect response behavior
- `.Accepts<T>()` documents request body types but doesn't enable automatic parsing
- These methods only support **single content types per call** (typically `application/json`)

**HATEOAS Challenge:**
You cannot specify multiple content types like:
```csharp
.Produces<StoryResponse>("application/json")
.Produces<Linked<StoryResponse>>("application/vnd.iheartfiction.hateoas+json")
```

#### OpenAPI Generation Limitations

The OpenAPI generator does not support multiple response schemas per endpoint based on content type. All responses are hardcoded to `application/json` in the generated specification, regardless of actual runtime behavior.

**GitHub Issue:** [dotnet/aspnetcore#56177](https://github.com/dotnet/aspnetcore/issues/56177) tracks this limitation.

#### Current Project Decision

Due to these limitations, this project has adopted a **universal HATEOAS approach** where all API responses include hypermedia links by default, rather than requiring clients to opt-in via specific `Accept` headers. This eliminates the content negotiation problem entirely while providing consistent hypermedia support.

This decision may be revisited as the framework evolves and better content negotiation support becomes available.

#### Alternative Approaches (If Content Negotiation Needed)

**Manual Content Negotiation:**
```csharp
public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
{
    return builder.MapGet("/stories/{id}", async (
        HttpContext context,
        [FromRoute] Ulid id,
        GetStoryUseCase useCase) =>
    {
        var result = await useCase.HandleAsync(id);
        
        var acceptHeader = context.Request.Headers.Accept.ToString();
        
        if (acceptHeader.Contains("application/vnd.iheartfiction.hateoas+json"))
        {
            return Results.Ok(result.ToLinked()); // HATEOAS-enhanced
        }
        
        return Results.Ok(result.Value); // Plain response
    })
    .Produces<StoryResponse>("application/json")
    .Produces<Linked<StoryResponse>>("application/vnd.iheartfiction.hateoas+json"); // Documentation only
}
```

**Separate Endpoints:**
```csharp
// Standard endpoint
builder.MapGet("/stories/{id}", standardHandler)
    .Produces<StoryResponse>();

// HATEOAS endpoint  
builder.MapGet("/stories/{id}/hateoas", hateoasHandler)
    .Produces<Linked<StoryResponse>>();
```

#### Performance and AOT Implications

**AOT Compatibility:** Manual content negotiation doesn't require reflection and remains AOT-compatible. `HttpContext.Request.Headers.Accept` access is AOT-safe.

**Performance:** String parsing of `Accept` headers has minimal overhead and can be optimized with `ReadOnlySpan<char>` operations.

#### Framework Evolution Outlook

Microsoft has acknowledged this limitation. Potential future solutions being discussed include:
- Enhanced `.Produces()` overloads with content type parameters
- Built-in content negotiation middleware for Minimal APIs  
- Improved OpenAPI generation supporting multiple response schemas per endpoint

Until these improvements are available, manual content negotiation or architectural decisions (like universal HATEOAS) remain the primary solutions.

### MongoDB.EntityFrameworkCore Package Compatibility Issues

The `MongoDB.EntityFrameworkCore` package presents significant compatibility challenges when upgrading to .NET 10 and pursuing AOT compatibility.

#### EF Core 10 Compatibility Problems

While the package **partially works** with EF Core 10, several critical issues emerge:

**Missing Method Implementations:**
- Discriminator property methods are missing, causing runtime errors
- This occurs even with simple, non-polymorphic models that shouldn't require discriminators
- Error manifests despite having only a single model type stored in MongoDB

**LINQ Async Method Gaps:**
- Standard EF Core async methods (like `ToListAsync()`, `FirstOrDefaultAsync()`) are not implemented
- The underlying MongoDB driver lacks these EF Core-style async method implementations
- Forces inconsistent data access patterns between SQL (PostgreSQL) and NoSQL (MongoDB) contexts

#### Current Workaround Strategy

**Bypass EF Core Abstraction:**
```csharp
// Instead of EF Core style:
var stories = await context.Stories.Where(s => s.UserId == userId).ToListAsync();

// Must use MongoDB driver directly:
var stories = await collection.Find(s => s.UserId == userId).ToListAsync();
```

**Package Dependency Issue:**
The package must remain installed solely for the `ObjectIdJsonConverter`, which is required for:
- Serializing MongoDB `ObjectId` types stored in PostgreSQL as strings
- Maintaining consistent JSON serialization across the application
- Cross-database referential integrity between PostgreSQL and MongoDB entities

#### AOT Compatibility Impact

This situation complicates AOT preparation:
- Cannot achieve full EF Core API consistency across data contexts
- Mixed data access patterns increase complexity and maintenance burden
- Package dependency exists primarily for JSON converter utility rather than actual EF Core functionality

#### Future Resolution Path

**Upgrade Dependency:**
- Waiting for MongoDB.EntityFrameworkCore to officially support EF Core 10
- Will require refactoring back to EF Core-style APIs once compatibility is restored
- Need to maintain two data access patterns until then

**Alternative Considered:**
- Custom `ObjectIdJsonConverter` implementation to eliminate package dependency
- Would allow complete removal of problematic package
- Deferred due to additional development complexity

#### Impact on Architecture

This limitation forces **inconsistent data access patterns** across the application:
- PostgreSQL contexts use standard EF Core APIs and patterns  
- MongoDB contexts bypass EF Core and use native driver methods directly
- Complicates repository patterns and unit testing strategies
- Requires team knowledge of both EF Core and MongoDB driver APIs

This inconsistency will persist until the MongoDB.EntityFrameworkCore package receives proper EF Core 10 support and comprehensive async method implementations.
