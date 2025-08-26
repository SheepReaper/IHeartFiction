## Design Philosophy

The architecture of IHeartFiction is guided by several key principles:

- **Vertical Slice Architecture:** Instead of organizing code by technical layers (e.g., "Controllers", "Services", "Data"), we group features by vertical slices. Each feature, like `CreateStory` or `GetAuthorProfile`, is a self-contained unit. This makes the codebase easier to understand, maintain, and extend.
- **CQRS (Command Query Responsibility Segregation) Lite:** We separate operations that change data (Commands) from operations that read data (Queries). This separation allows us to optimize each path independently, leading to better performance and clarity. For example, a `CreateStory` command is handled differently than a `ListPublishedStories` query.
- **Shared Kernel:** Common code, entities, and interfaces that are shared across different parts of the system are placed in a `SharedKernel` library. This promotes code reuse while minimizing tight coupling between services.
- **Primary Key Strategy: ULIDs**: For entity identifiers, we have made a deliberate choice to use ULIDs (Universally Unique Lexicographically Sortable Identifiers) instead of traditional integers or GUIDs. This decision provides several key advantages that align with the goals of a modern, scalable web platform:
    - **Performance:** Unlike GUIDs (UUIDv4), ULIDs are monotonic; they are generated in a lexicographically sortable order based on their timestamp component. This is a major benefit for database performance, as it prevents index fragmentation on our primary keys and clustered indexes, leading to faster writes and range queries.
    - **Usability:** ULIDs are URL-safe and more compact when represented as strings compared to GUIDs, making for cleaner and slightly shorter API routes.
    - **Best of Both Worlds:** They combine the global uniqueness of a GUID with the sortable, time-based nature of a sequential integer ID, without exposing internal row counts or creating contention issues in a distributed system.

## Why .NET 10?

Choosing to build on a pre-release version of .NET 10 (`<TargetFramework>net10.0</TargetFramework>`) is a deliberate, forward-looking decision. It allows us to build with the latest tooling and take advantage of modern C# and ASP.NET Core features that streamline development, improve performance, and enhance code clarity.

Unlike previous versions, .NET 10 provides first-class features that directly simplify our vertical slice architecture. Here are several specific examples:

### Enhanced Validation

- **Built-in Minimal API Validation**: .NET 10 introduces streamlined validation support. We leverage this globally by registering it with `builder.Services.AddValidation()`. This service automatically validates request models, including **record types**, based on their `DataAnnotation` attributes, returning a standardized `ProblemDetails` response on failure. This removes boilerplate code from our handlers and integrates perfectly with our architecture.
- **Improved Form Validation**: The framework also enhances server-side validation for form submissions, providing a structured way to return errors that can be easily bound to client-side forms. While not yet implemented, this provides a powerful option for future UIs.

### Advanced OpenAPI Integration

- **OpenAPI 3.1 Support**: The project is configured to generate an **OpenAPI 3.1** document, allowing us to leverage the latest features of the specification.
- **Rich Documentation from Code**: XML documentation comments (e.g., `<summary>`) in our endpoint classes are now automatically merged into the generated OpenAPI document. We further customize this with document and schema transformers for **dynamic schema generation**, resulting in rich, accurate, and always up-to-date API documentation with minimal effort.

### Modern C# 14 Language Features

- The codebase takes advantage of new C# 14 features that improve clarity and reduce verbosity. For example, we use the new ability to apply the `nameof()` operator to **unbound generic types** (e.g., `nameof(List<>)`), which is useful in logging and reflection scenarios.

### Future-Proofing the Architecture

By building on .NET 10, we are also positioned to easily adopt other powerful upcoming features with minimal friction. Features we plan to implement soon include:
- **Passkey Support**: Leveraging the new WebAuthn and FIDO2 standards built into ASP.NET Core Identity for passwordless authentication.
- **JSON Patch**: Implementing partial resource updates using `application/json-patch+json` for more efficient API interactions.

## Architectural API Features

The API is built with a rich set of cross-cutting features that ensure consistency, discoverability, and a robust client development experience. Instead of just a list of endpoints, it's more useful to describe the common functionality available across the API.

### Standardized Querying & Data Manipulation
For any endpoint that returns a collection of resources, we provide a common set of capabilities exposed via query parameters. Business-specific logic is reserved for the request body.

- **Pagination:** Collections are paginated by default. Clients can control paging using the `page` and `pageSize` query parameters.
- **Sorting:** Clients can specify sort order for resource collections via the `sort` query parameter (e.g., `sort=title asc, createdDate desc`).
- **Data Shaping:** Clients can request a subset of fields to reduce payload size by supplying a `fields` query parameter (e.g., `fields=id,title,author`).
- **Searching:** A `q (search)` parameter is available on supported endpoints for simple keyword-based searches.
- **Filtering:** The architecture supports advanced filtering, though it is not yet implemented on all endpoints.

### Hypermedia-Driven Responses (HATEOAS)
We embrace HATEOAS principles to improve API discoverability. Where supported, responses include hypermedia links that guide the client to related actions or resources. This is present on collection-level responses and on individual members within the collection, reducing the need for clients to hardcode URI patterns.

### Standardized Error Handling
The API uses `ProblemDetails` (RFC 7807) for all error responses. This provides a consistent, machine-readable error format, which simplifies client-side error handling logic.

### Code-First API with Typed Clients
We follow a "code-first" approach to API development, where the C# source code is the single source of truth. The OpenAPI specification is a generated artifact that enables a robust client development experience.

1.  **Live Schema Generation:** On every build, the API generates an `openapi.json` document from its endpoint metadata and data contracts. This serves as a live, accurate contract for all available operations.
2.  **Interactive API Documentation:** We use Scalar UI to automatically generate a beautiful, interactive API documentation website from the generated OpenAPI schema. This allows developers to explore and test API endpoints directly from their browser during development.
3.  **Typed Client Generation:** The generated schema is then consumed by our Blazor Web App. A source generator uses it to create a strongly-typed C# HTTP client, providing compile-time safety for API calls, enables IntelliSense, and eliminates a whole class of common frontend bugs.

This approach was chosen deliberately. Even though the initial Blazor app runs in Interactive Server mode, using a proper HTTP client decouples the frontend from the backend, making it significantly easier to introduce other clients in the future, such as a Blazor WASM app or a native .NET MAUI application, without having to refactor the presentation layer.

## A Note on Minimal APIs

We have chosen to use ASP.NET Core's Minimal APIs to build our backend. This decision was driven by the desire for high performance, reduced boilerplate, and simplicity, which is perfect for the well-defined, single-purpose endpoints that characterize a vertical slice architecture.

However, this choice comes with certain trade-offs. Minimal APIs, by design, are less opinionated and provide less out-of-the-box structure compared to the traditional MVC (Model-View-Controller) framework. For example, features like complex model binding from multiple sources or deep filter integration require more manual setup than in an MVC controller.

Another current limitation is the lack of built-in support for content negotiation. While the OpenAPI specification allows defining different response models per content type (e.g., `application/json` vs. `application/vnd.iheartfiction.hateoas+json`), the OpenAPI generator in ASP.NET Core does not yet fully support this (see [dotnet/aspnetcore#56177](https://github.com/dotnet/aspnetcore/issues/56177)). Due to this tooling limitation, we have made the design decision to forgo content negotiation for the time being. Consequently, all API responses include hypermedia (HATEOAS) links by default, rather than requiring clients to opt-in via a specific `Accept` header. This approach may be revisited as the framework evolves.

For the initial feature set, the benefits of performance and simplicity offered by Minimal APIs far outweigh these limitations. The focused nature of our endpoints aligns perfectly with the minimal approach. As the platform grows, we will continuously evaluate this choice to ensure it remains the best fit for our needs.
