# Parameter Binding Interfaces

This directory contains standardized interfaces for common query parameter patterns used across minimal API endpoints.

## Overview

The interfaces in this directory implement a composition pattern that allows endpoints to declare their parameter requirements through interface implementation, enabling:

- **Automatic validation** via .NET 10's built-in validation system
- **Consistent parameter patterns** across all endpoints
- **Type safety** and IntelliSense support
- **Reusable validation logic**

## Available Interfaces

### `IPaginationSupport`
For endpoints that support pagination functionality.
- `Page` (int): 1-based page number, must be > 0
- `PageSize` (int): Items per page, must be 1-100

### `ISortingSupport`
For endpoints that support sorting functionality.
- `SortBy` (string): Field to sort by
- `SortOrder` (string): Sort direction (asc/desc)

### `ISearchSupport`
For endpoints that support optional search functionality.
- `Search` (string?): Optional search query, 2-100 characters when provided

### `IRequiredSearchSupport`
For endpoints that require a search query.
- `Q` (string?): Required search query, 2-100 characters

### `IFilterSupport`
For endpoints that support filtering functionality.
- `Filter` (string?): Optional filter criteria, max 200 characters

## Usage Pattern

### 1. Implement Required Interfaces
```csharp
internal sealed class ListAuthorsRequest : IPaginationSupport, ISortingSupport, ISearchSupport
{
    // Interface implementations
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "name";
    public string SortOrder { get; init; } = "asc";
    public string? Search { get; init; }
    
    // Endpoint-specific properties (if any)
    public bool IncludeInactive { get; init; } = false;
}
```

### 2. Use [AsParameters] in Endpoint
```csharp
builder.MapGet("authors", async (
    [AsParameters] ListAuthorsRequest request,
    ListAuthors useCase,
    CancellationToken cancellationToken) =>
{
    // No manual validation needed - .NET 10 handles it automatically
    var result = await useCase.HandleAsync(request, cancellationToken);
    return ResponseMappingService.MapToHttpResult(result);
});
```

## Benefits

- **Automatic Validation**: .NET 10's `AddValidation()` automatically validates all parameters
- **400 Bad Request**: Invalid data automatically returns proper error responses
- **No Manual Validation**: Eliminates redundant validation code in endpoints
- **Consistent Patterns**: All endpoints use the same parameter patterns
- **Type Safety**: Full IntelliSense and compile-time checking

## Architecture Separation

- **Query Parameters** (`[AsParameters]`): Reserved for common, reusable functionality (pagination, sorting, filtering, search)
- **Request Body** (`[FromBody]`): Reserved for endpoint-specific business data and operations
