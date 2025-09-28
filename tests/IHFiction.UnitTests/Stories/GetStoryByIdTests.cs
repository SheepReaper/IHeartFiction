using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for GetStoryById functionality
/// Tests the endpoint constants and basic functionality patterns
/// </summary>
public class GetStoryByIdTests
{


    [Fact]
    public void GetStoryByIdResponse_CanBeCreated()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var ownerId = Ulid.NewUlid();
        var authorId = Ulid.NewUlid();
        var now = DateTime.UtcNow;
        var authors = new[] { new GetPublishedStory.StoryAuthor(authorId, "Test Author") };
        var tags = new[] { new GetPublishedStory.StoryTag("Genre", null, "Fantasy") };

        // Act
        var response = new GetPublishedStory.GetPublishedStoryResponse(
            id,
            "Test Story",
            "Test Description",
            now,
            true,
            now,
            now.AddDays(-1),
            ownerId,
            "Owner Name",
            StoryType.SingleBody,
            authors,
            tags,
            [],
            []);

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal("Test Story", response.Title);
        Assert.Equal("Test Description", response.Description);
        Assert.Equal(now, response.PublishedAt);
        Assert.True(response.IsPublished);
        Assert.Equal(now, response.UpdatedAt);
        Assert.Equal(now.AddDays(-1), response.CreatedAt);
        Assert.Equal(ownerId, response.OwnerId);
        Assert.Equal("Owner Name", response.OwnerName);
        Assert.Single(response.Authors);
        Assert.Single(response.Tags);
    }

    [Fact]
    public void StoryAuthor_CanBeCreated()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var name = "Test Author";

        // Act
        var author = new GetPublishedStory.StoryAuthor(id, name);

        // Assert
        Assert.Equal(id, author.Id);
        Assert.Equal(name, author.Name);
    }

    [Fact]
    public void StoryTag_CanBeCreatedWithoutSubcategory()
    {
        // Arrange
        var category = "Genre";
        var value = "Fantasy";

        // Act
        var tag = new GetPublishedStory.StoryTag(category, null, value);

        // Assert
        Assert.Equal(category, tag.Category);
        Assert.Null(tag.Subcategory);
        Assert.Equal(value, tag.Value);
    }

    [Fact]
    public void StoryTag_CanBeCreatedWithSubcategory()
    {
        // Arrange
        var category = "Content";
        var subcategory = "Warning";
        var value = "Violence";

        // Act
        var tag = new GetPublishedStory.StoryTag(category, subcategory, value);

        // Assert
        Assert.Equal(category, tag.Category);
        Assert.Equal(subcategory, tag.Subcategory);
        Assert.Equal(value, tag.Value);
    }

    [Fact]
    public void GetStoryByIdResponse_WithEmptyCollections_WorksCorrectly()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var ownerId = Ulid.NewUlid();
        var now = DateTime.UtcNow;
        var emptyAuthors = Array.Empty<GetPublishedStory.StoryAuthor>();
        var emptyTags = Array.Empty<GetPublishedStory.StoryTag>();

        // Act
        var response = new GetPublishedStory.GetPublishedStoryResponse(
            id,
            "Test Story",
            "Test Description",
            null,
            false,
            now,
            now.AddDays(-1),
            ownerId,
            "Owner Name",
            StoryType.SingleBody,
            emptyAuthors,
            emptyTags,
            [],
            []);

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Null(response.PublishedAt);
        Assert.False(response.IsPublished);
        Assert.Empty(response.Authors);
        Assert.Empty(response.Tags);
    }

    [Fact]
    public void GetStoryByIdResponse_WithMultipleAuthorsAndTags_WorksCorrectly()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var ownerId = Ulid.NewUlid();
        var now = DateTime.UtcNow;
        var authors = new[]
        {
            new GetPublishedStory.StoryAuthor(Ulid.NewUlid(), "Author One"),
            new GetPublishedStory.StoryAuthor(Ulid.NewUlid(), "Author Two")
        };
        var tags = new[]
        {
            new GetPublishedStory.StoryTag("Genre", null, "Fantasy"),
            new GetPublishedStory.StoryTag("Content", "Warning", "Violence"),
            new GetPublishedStory.StoryTag("Rating", null, "Teen")
        };

        // Act
        var response = new GetPublishedStory.GetPublishedStoryResponse(
            id,
            "Collaborative Story",
            "A story with multiple authors and tags",
            now,
            true,
            now,
            now.AddDays(-1),
            ownerId,
            "Primary Owner",
            StoryType.MultiChapter,
            authors,
            tags,
            [],
            []);

        // Assert
        Assert.Equal(2, response.Authors.Count());
        Assert.Equal(3, response.Tags.Count());
    }
}
