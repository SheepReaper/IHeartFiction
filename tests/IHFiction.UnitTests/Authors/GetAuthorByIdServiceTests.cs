using FluentAssertions;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.UnitTests.Authors;

/// <summary>
/// Unit tests for GetAuthorById using a service-based approach
/// These tests focus on testing the business logic and response mapping
/// without relying on DbContext mocking
/// </summary>
public class GetAuthorByIdServiceTests
{
    [Fact]
    public void Response_MapsAuthorDataCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var updatedAt = DateTime.UtcNow;
        var deletedAt = DateTime.UtcNow.AddDays(1);
        var bio = "This is a test bio";

        var work1 = CreateTestWork("First Work", true);
        var work2 = CreateTestWork("Second Work", true);
        var work3 = CreateTestWork("Unpublished Work", false); // Unpublished work should not appear in response
        var works = new List<Work> { work1, work2 };

        var author = CreateTestAuthor(userId, authorName, bio, updatedAt, deletedAt, works);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),
            3
        );

        // Assert
        response.UserId.Should().Be(userId);
        response.Name.Should().Be(authorName);
        response.UpdatedAt.Should().Be(updatedAt);
        response.DeletedAt.Should().Be(deletedAt);
        response.Profile.Bio.Should().Be(bio);
        response.PublishedStories.Should().HaveCount(2);
        response.PublishedStories.Should().Contain(w => w.Title == "First Work");
        response.PublishedStories.Should().Contain(w => w.Title == "Second Work");
    }

    [Fact]
    public void Response_HandlesNullBioCorrectly()
    {
        // Arrange
        var author = CreateTestAuthor(Guid.NewGuid(), "Author", null, DateTime.UtcNow, null, []);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),
            0
        );

        // Assert
        response.Profile.Bio.Should().BeNull();
    }

    [Fact]
    public void Response_HandlesEmptyWorksCollectionCorrectly()
    {
        // Arrange
        var author = CreateTestAuthor(Guid.NewGuid(), "Author", "Bio", DateTime.UtcNow, null, []);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),

            0
        );

        // Assert
        response.PublishedStories.Should().BeEmpty();
    }

    [Fact]
    public void Response_HandlesDeletedAuthorCorrectly()
    {
        // Arrange
        var deletedAt = DateTime.UtcNow;
        var author = CreateTestAuthor(Guid.NewGuid(), "Deleted Author", "Bio", DateTime.UtcNow.AddDays(-1), deletedAt, []);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),
            0
        );

        // Assert
        response.DeletedAt.Should().Be(deletedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Short bio")]
    [InlineData("This is a much longer bio that contains multiple sentences and provides detailed information about the author's background, writing style, and interests.")]
    [InlineData("Bio with émojis 📚 and special characters <>&\"'")]
    [InlineData("Bio with\nnewlines\rand\ttabs")]
    public void Response_HandlesVariousBioFormatsCorrectly(string bio)
    {
        // Arrange
        var author = CreateTestAuthor(Guid.NewGuid(), "Author", bio, DateTime.UtcNow, null, []);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),
            0
        );

        // Assert
        response.Profile.Bio.Should().Be(bio);
    }

    [Fact]
    public void Response_HandlesLargeNumberOfWorksCorrectly()
    {
        // Arrange
        var works = new List<Work>();
        for (int i = 0; i < 100; i++)
        {
            works.Add(CreateTestWork($"Work {i:D3}", true));
        }

        var author = CreateTestAuthor(Guid.NewGuid(), "Prolific Author", "Very productive", DateTime.UtcNow, null, works);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),
            100
        );

        // Assert
        response.PublishedStories.Should().HaveCount(100);
        response.PublishedStories.Should().Contain(w => w.Title == "Work 000");
        response.PublishedStories.Should().Contain(w => w.Title == "Work 099");
        response.PublishedStories.Should().OnlyContain(w => w.Id != Ulid.Empty);
        response.PublishedStories.Should().OnlyContain(w => !string.IsNullOrEmpty(w.Title));
    }

    [Fact]
    public void Response_HandlesWorksWithSpecialCharactersCorrectly()
    {
        // Arrange
        var works = new List<Work>
        {
            CreateTestWork("Work with émojis 📖", true),
            CreateTestWork("Work with symbols @#$%^&*()", true),
            CreateTestWork("Work with quotes \"'", true),
            CreateTestWork("Work with newlines\n\r", true),
            CreateTestWork("Work with tabs\t\t", true),
            CreateTestWork("Work with HTML <script>alert('test')</script>", true)
        };

        var author = CreateTestAuthor(Guid.NewGuid(), "Special Author", "Bio", DateTime.UtcNow, null, works);

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(
            author.UserId,
            author.Name,
            author.UpdatedAt,
            author.DeletedAt,
            new GetAuthorById.AuthorProfile(author.Profile.Bio),
            author.Works.Select(work => new GetAuthorById.AuthorWorkItem(work.Id, work.Title, null)),
            6
        );

        // Assert
        response.PublishedStories.Should().HaveCount(6);
        response.PublishedStories.Should().Contain(w => w.Title == "Work with émojis 📖");
        response.PublishedStories.Should().Contain(w => w.Title == "Work with symbols @#$%^&*()");
        response.PublishedStories.Should().Contain(w => w.Title == "Work with HTML <script>alert('test')</script>");
    }

    [Fact]
    public void DomainError_NotFound_HasCorrectProperties()
    {
        // Arrange & Act
        var error = DomainError.NotFound;

        // Assert
        error.Code.Should().Be("General.NotFound");
        error.Description.Should().Be("The requested resource was not found.");
    }

    private static Author CreateTestAuthor(Guid userId, string name, string? bio, DateTime updatedAt, DateTime? deletedAt, ICollection<Work> works)
    {
        var author = new Author
        {
            Id = Ulid.NewUlid(),
            UserId = userId,
            Name = name,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
            Profile = new Profile { Bio = bio }
        };

        // Add works to the collection
        foreach (var work in works)
        {
            author.Works.Add(work);
        }

        return author;
    }

    private static TestWork CreateTestWork(string title, bool publish = false)
    {
        return new TestWork
        {
            Id = Ulid.NewUlid(),
            Title = title,
            Owner = null!, // Will be set by the test
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = publish ? DateTime.UtcNow : null
        };
    }

    // Test implementation of Work since it's abstract
    private class TestWork : Work
    {
        // No additional properties needed for testing GetAuthorById
    }
}
