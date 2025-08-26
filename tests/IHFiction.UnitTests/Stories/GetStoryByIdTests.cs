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
            authors,
            tags,
            false,
            false,
            false,
            true);

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
        Assert.False(response.HasContent);
        Assert.False(response.HasChapters);
        Assert.False(response.HasBooks);
        Assert.True(response.IsValid);
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
            emptyAuthors,
            emptyTags,
            false,
            false,
            false,
            true);

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
            authors,
            tags,
            true,
            true,
            false,
            true);

        // Assert
        Assert.Equal(2, response.Authors.Count());
        Assert.Equal(3, response.Tags.Count());
        Assert.True(response.HasContent);
        Assert.True(response.HasChapters);
        Assert.False(response.HasBooks);
        Assert.True(response.IsValid);
    }

    [Fact]
    public void GetStoryByIdResponse_StoryValidationStates_ReflectCorrectly()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var ownerId = Ulid.NewUlid();
        var now = DateTime.UtcNow;
        var authors = Array.Empty<GetPublishedStory.StoryAuthor>();
        var tags = Array.Empty<GetPublishedStory.StoryTag>();

        // Act & Assert - New story (no content, chapters, or books)
        var newStory = new GetPublishedStory.GetPublishedStoryResponse(
            id, "New Story", "Description", null, false, now, now, ownerId, "Owner",
            authors, tags, false, false, false, true);
        Assert.True(newStory.IsValid);

        // Act & Assert - One shot (has content, no chapters or books)
        var oneShot = new GetPublishedStory.GetPublishedStoryResponse(
            id, "One Shot", "Description", null, false, now, now, ownerId, "Owner",
            authors, tags, true, false, false, true);
        Assert.True(oneShot.IsValid);

        // Act & Assert - Serial story (has chapters, no content or books)
        var serial = new GetPublishedStory.GetPublishedStoryResponse(
            id, "Serial", "Description", null, false, now, now, ownerId, "Owner",
            authors, tags, false, true, false, true);
        Assert.True(serial.IsValid);

        // Act & Assert - Book series (has books, no content or chapters)
        var series = new GetPublishedStory.GetPublishedStoryResponse(
            id, "Series", "Description", null, false, now, now, ownerId, "Owner",
            authors, tags, false, false, true, true);
        Assert.True(series.IsValid);
    }
}
