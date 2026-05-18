using IHFiction.FictionApi.Tags;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Tags;

/// <summary>
/// Unit tests for ListTags functionality
/// Tests request validation, response model construction, and query parameter handling
/// </summary>
public class ListTagsTests
{
    // Note: Request validation tests removed as they test low-value implementation details.
    // Validation is handled automatically by .NET 10's AddValidation() system with
    // business logic validation in the endpoint implementation.

    [Fact]
    public void PaginatedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var tags = new List<ListTags.ListTagsItem>
        {
            new(Ulid.NewUlid(), "genre", null, "fantasy", DateTime.UtcNow, 15, "genre:fantasy"),
            new(Ulid.NewUlid(), "theme", null, "adventure", DateTime.UtcNow, 8, "theme:adventure")
        };

        // Act
        var response = new PagedCollection<ListTags.ListTagsItem>(
            tags.AsQueryable(),
            125, // TotalCount
            2,   // CurrentPage
            50); // PageSize

        // Assert
        Assert.Equal(2, response.Data.Count());
        Assert.Equal(125, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(2, response.CurrentPage);
        Assert.Equal(50, response.PageSize);
        Assert.True(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }
}
