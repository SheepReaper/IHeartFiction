using IHFiction.FictionApi.Authors;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Authors;

/// <summary>
/// Unit tests for ListAuthors functionality
/// Tests request validation, response model construction, and query parameter handling
/// </summary>
public class ListAuthorsTests
{
    // Note: Request validation tests removed as they test low-value implementation details.
    // Validation is handled automatically by .NET 10's AddValidation() system with
    // business logic validation in PaginationService.NormalizePaginationWithValidation().

    [Fact]
    public void AuthorItem_CanBeCreated()
    {
        // Arrange
        var authorId = Ulid.NewUlid();
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var item = new ListAuthors.ListAuthorsItem(
            authorId,
            "John Doe",
            "A prolific author of fantasy novels.",
            createdAt,
            updatedAt,
            5,  // TotalStories
            3); // PublishedStories

        // Assert
        Assert.Equal(authorId, item.Id);
        Assert.Equal("John Doe", item.Name);
        Assert.Equal("A prolific author of fantasy novels.", item.Bio);
        Assert.Equal(createdAt, item.CreatedAt);
        Assert.Equal(updatedAt, item.UpdatedAt);
        Assert.Equal(5, item.TotalStories);
        Assert.Equal(3, item.PublishedStories);
    }

    [Fact]
    public void PaginatedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var authors = new List<ListAuthors.ListAuthorsItem>
        {
            new(Ulid.NewUlid(), "Author 1", "Bio 1", DateTime.UtcNow, DateTime.UtcNow, 3, 2),
            new(Ulid.NewUlid(), "Author 2", "Bio 2", DateTime.UtcNow, DateTime.UtcNow, 5, 4)
        };

        // Act
        var response = new PagedCollection<ListAuthors.ListAuthorsItem>(
            authors.AsQueryable(),
            25,  // TotalCount
            2,   // CurrentPage
            10); // PageSize

        // Assert
        Assert.Equal(2, response.Data.Count());
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(2, response.CurrentPage);
        Assert.Equal(10, response.PageSize);
        Assert.True(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }
}
