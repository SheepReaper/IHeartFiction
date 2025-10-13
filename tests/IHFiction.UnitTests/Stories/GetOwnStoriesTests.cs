using IHFiction.FictionApi.Account;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for GetOwnStories functionality
/// Tests request validation and response model construction
/// </summary>
public class GetOwnStoriesTests
{
    [Fact]
    public void AuthorStoryItem_CanBeCreated()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var now = DateTime.UtcNow;
        var collaboratorNames = new[] { "Collaborator One", "Collaborator Two" };
        var tags = new[] { "fantasy", "adventure" };

        // Act
        var item = new GetOwnStories.AuthorStoryItem(
            id,
            "Test Story",
            "Test Description",
            now,
            true,
            now,
            now.AddDays(-1),
            true,
            collaboratorNames,
            tags,
            true,
            false,
            false,
            true);

        // Assert
        Assert.Equal(id, item.Id);
        Assert.Equal("Test Story", item.Title);
        Assert.Equal("Test Description", item.Description);
        Assert.Equal(now, item.PublishedAt);
        Assert.True(item.IsPublished);
        Assert.True(item.IsOwned);
        Assert.Equal(2, item.CollaboratorNames.Count());
        Assert.Equal(2, item.Tags.Count());
        Assert.True(item.HasContent);
        Assert.False(item.HasChapters);
        Assert.False(item.HasBooks);
        Assert.True(item.IsValid);
    }

    [Fact]
    public void AuthorStoryItem_WithCollaborativeStory_ShowsCorrectOwnership()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var now = DateTime.UtcNow;
        var collaboratorNames = new[] { "Story Owner" };
        var tags = new[] { "collaborative" };

        // Act
        var item = new GetOwnStories.AuthorStoryItem(
            id,
            "Collaborative Story",
            "A story where I'm a collaborator",
            null,
            false,
            now,
            now.AddDays(-1),
            false, // Not owned by current author
            collaboratorNames,
            tags,
            false,
            true,
            false,
            true);

        // Assert
        Assert.False(item.IsOwned);
        Assert.False(item.IsPublished);
        Assert.Single(item.CollaboratorNames);
        Assert.Equal("Story Owner", item.CollaboratorNames.First());
    }

    [Fact]
    public void PaginatedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var stories = new[]
        {
            new GetOwnStories.AuthorStoryItem(
                Ulid.NewUlid(),
                "Story 1",
                "Description 1",
                DateTime.UtcNow,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(-1),
                true,
                ["Collaborator 1"],
                ["tag1"],
                true,
                false,
                false,
                true)
        };

        // Act
        var response = new PagedCollection<GetOwnStories.AuthorStoryItem>(
            stories.AsQueryable(),
            25,
            2,
            10);

        // Assert
        Assert.Single(response.Data);
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(2, response.CurrentPage);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(3, response.TotalPages);
        Assert.True(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }

    [Fact]
    public void PaginatedCollectionResponse_WithFirstPage_HasCorrectPaginationFlags()
    {
        // Arrange
        var stories = Array.Empty<GetOwnStories.AuthorStoryItem>();

        // Act
        var response = new PagedCollection<GetOwnStories.AuthorStoryItem>(
            stories.AsQueryable(),
            15,
            1,
            10);

        // Assert
        Assert.True(response.HasNextPage);
        Assert.False(response.HasPreviousPage);
    }

    [Fact]
    public void PaginatedCollectionResponse_WithLastPage_HasCorrectPaginationFlags()
    {
        // Arrange
        var stories = Array.Empty<GetOwnStories.AuthorStoryItem>();

        // Act
        var response = new PagedCollection<GetOwnStories.AuthorStoryItem>(
            stories.AsQueryable(),
            15,
            2,
            10);

        // Assert
        Assert.False(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }
}