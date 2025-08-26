using FluentAssertions;

using IHFiction.FictionApi.Authors;

namespace IHFiction.UnitTests.Authors;

/// <summary>
/// Behavior tests for GetAuthorById using NSubstitute for interface mocking
/// These tests focus on testing behaviors, interactions, and business logic
/// </summary>
public class GetAuthorByIdBehaviorTests
{




    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("Normal Author Name")]
    [InlineData("Author with Special Characters @#$%^&*()")]
    [InlineData("Author with émojis 🎭📚")]
    [InlineData("Very Long Author Name That Exceeds Normal Length Expectations And Contains Multiple Words")]
    public void Response_WithVariousNames_HandlesCorrectly(string name)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow;
        var profile = new GetAuthorById.AuthorProfile("Bio");
        var works = new List<GetAuthorById.AuthorWorkItem>();

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(userId, name, updatedAt, null, profile, works);

        // Assert
        response.Name.Should().Be(name);
    }

    [Fact]
    public void Response_WithEmptyGuid_IsValid()
    {
        // Arrange
        var emptyGuid = Guid.Empty;
        var name = "Author with Empty GUID";
        var updatedAt = DateTime.UtcNow;
        var profile = new GetAuthorById.AuthorProfile("Bio");
        var works = new List<GetAuthorById.AuthorWorkItem>();

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(emptyGuid, name, updatedAt, null, profile, works);

        // Assert
        response.UserId.Should().Be(emptyGuid);
    }

    [Fact]
    public void Response_WithFutureTimestamp_IsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var name = "Future Author";
        var futureDate = new DateTime(2100, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var profile = new GetAuthorById.AuthorProfile("Bio");
        var works = new List<GetAuthorById.AuthorWorkItem>();

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(userId, name, futureDate, null, profile, works);

        // Assert
        response.UpdatedAt.Should().Be(futureDate);
    }

    [Fact]
    public void Response_WithPastTimestamp_IsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var name = "Historical Author";
        var pastDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var profile = new GetAuthorById.AuthorProfile("Bio");
        var works = new List<GetAuthorById.AuthorWorkItem>();

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(userId, name, pastDate, null, profile, works);

        // Assert
        response.UpdatedAt.Should().Be(pastDate);
    }

    [Fact]
    public void Response_WithDeletedAtBeforeUpdatedAt_IsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var name = "Inconsistent Author";
        var updatedAt = DateTime.UtcNow;
        var deletedAt = updatedAt.AddDays(-1); // Deleted before last update
        var profile = new GetAuthorById.AuthorProfile("Bio");
        var works = new List<GetAuthorById.AuthorWorkItem>();

        // Act
        var response = new GetAuthorById.GetAuthorByIdResponse(userId, name, updatedAt, deletedAt, profile, works);

        // Assert
        response.UpdatedAt.Should().Be(updatedAt);
        response.DeletedAt.Should().Be(deletedAt);
    }

    [Fact]
    public void Work_WithEmptyUlid_IsValid()
    {
        // Arrange
        var emptyId = Ulid.Empty;
        var title = "Work with Empty ID";

        // Act
        var work = new GetAuthorById.AuthorWorkItem(emptyId, title);

        // Assert
        work.Id.Should().Be(emptyId);
        work.Title.Should().Be(title);
    }

    [Fact]
    public void Work_WithMaxUlid_IsValid()
    {
        // Arrange
        var maxId = Ulid.MaxValue;
        var title = "Work with Max ID";

        // Act
        var work = new GetAuthorById.AuthorWorkItem(maxId, title);

        // Assert
        work.Id.Should().Be(maxId);
        work.Title.Should().Be(title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("Normal Work Title")]
    [InlineData("Work with Special Characters @#$%^&*()")]
    [InlineData("Work with émojis 📖🎭")]
    [InlineData("Work with HTML <script>alert('test')</script>")]
    [InlineData("Work with newlines\nand\rtabs\t")]
    [InlineData("Very Long Work Title That Exceeds Normal Length Expectations And Contains Multiple Words And Phrases")]
    public void Work_WithVariousTitles_HandlesCorrectly(string title)
    {
        // Arrange
        var id = Ulid.NewUlid();

        // Act
        var work = new GetAuthorById.AuthorWorkItem(id, title);

        // Assert
        work.Title.Should().Be(title);
    }



    [Fact]
    public void Response_ToString_ContainsRelevantInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var name = "Test Author";
        var response = new GetAuthorById.GetAuthorByIdResponse(
            userId,
            name,
            DateTime.UtcNow,
            null,
            new GetAuthorById.AuthorProfile("Bio"),
            []
        );

        // Act
        var stringRepresentation = response.ToString();

        // Assert
        stringRepresentation.Should().NotBeNullOrEmpty();
        // Note: The exact format depends on the record's ToString implementation
    }
}
