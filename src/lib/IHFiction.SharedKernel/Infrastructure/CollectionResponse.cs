namespace IHFiction.SharedKernel.Infrastructure;

/// <summary>
/// A simple collection response
/// </summary>
public record CollectionResponse
{
    public static CollectionResponse<TData> Empty<TData>() => new(Enumerable.Empty<TData>().AsQueryable());
}

/// <summary>
/// Standardized response model for simple collections without pagination metadata.
/// Use this for endpoints that return collections but don't require pagination information.
/// </summary>
/// <typeparam name="TData">The type of items in the collection</typeparam>
/// <param name="Data">The collection of items</param>
public record CollectionResponse<TData>(
    IQueryable<TData> Data
) : CollectionResponse;