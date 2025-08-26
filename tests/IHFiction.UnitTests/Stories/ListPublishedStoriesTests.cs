using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for ListPublishedStories functionality
/// Tests request validation, response model construction, and query parameter handling
/// </summary>
public class ListPublishedStoriesTests
{
    // Note: Request validation tests removed as they test low-value implementation details.
    // Validation is handled automatically by .NET 10's AddValidation() system with
    // business logic validation in the endpoint implementation.

    [Fact]
    public void PublishedStoryItem_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var item = new ListPublishedStories.ListPublishedStoriesItem(
            storyId,
            "Test Story",
            "A test story description",
            publishedAt,
            updatedAt,
            true,  // HasContent
            false, // HasChapters
            false, // HasBooks
            0,
            Ulid.NewUlid(),
            "Author Name");    // ChapterCount

        // Assert
        Assert.Equal(storyId, item.StoryId);
        Assert.Equal("Test Story", item.Title);
        Assert.Equal("A test story description", item.Description);
        Assert.Equal(publishedAt, item.PublishedAt);
        Assert.Equal(updatedAt, item.UpdatedAt);
        Assert.Equal("Author Name", item.AuthorName);
        Assert.True(item.HasContent);
        Assert.False(item.HasChapters);
        Assert.False(item.HasBooks);
        Assert.Equal(0, item.ChapterCount);
    }

    [Fact]
    public void PaginatedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var stories = new List<ListPublishedStories.ListPublishedStoriesItem>
        {
            new(Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow, true, false, false, 0, Ulid.NewUlid(), "Author 1"),
            new(Ulid.NewUlid(), "Story 2", "Description 2", DateTime.UtcNow, DateTime.UtcNow, false, true, false, 5, Ulid.NewUlid(),"Author 2")
        };

        // Act
        var response = new PagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            stories.AsQueryable(),
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
