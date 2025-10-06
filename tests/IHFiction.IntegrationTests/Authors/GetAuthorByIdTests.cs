using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Integration tests for GetAuthorById functionality
/// Tests the complete use case handler and response mapping with database
/// </summary>
// [Collection(nameof(AuthorsTestCollection))]
public class GetAuthorByIdTests : BaseIntegrationTest, IConfigureServices<GetAuthorByIdTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly GetAuthorById _useCase;

    public GetAuthorByIdTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(GetAuthorByIdTests)) ?? throw new Exception("DbContext not found");
        _useCase = new GetAuthorById(_dbContext);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorExists_ReturnsSuccessWithCorrectResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var bio = "This is a test bio";
        var updatedAt = DateTime.UtcNow;
        var deletedAt = (DateTime?)null;

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
            Profile = new Profile { Bio = bio }
        };

        var work1 = new Story { Title = "Work 1", Description = "Test description 1", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        var work2 = new Story { Title = "Work 2", Description = "Test description 2", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };

        author.Works.Add(work1);
        author.Works.Add(work2);

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.UserId.Should().Be(userId);
        response.Name.Should().Be(authorName);
        response.UpdatedAt.Should().BeCloseTo(updatedAt, TimeSpan.FromSeconds(1));
        response.DeletedAt.Should().Be(deletedAt);
        response.Profile.Should().NotBeNull();
        response.Profile.Bio.Should().Be(bio);
        response.TotalStories.Should().Be(2);
        var works = response.PublishedStories;
        works.Should().HaveCount(2);
        works.Should().Contain(w => w.Title == "Work 1");
        works.Should().Contain(w => w.Title == "Work 2");
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var nonExistentId = Ulid.NewUlid();

        // Act
        var result = await _useCase.HandleAsync(nonExistentId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorHasNoBio_ReturnsResponseWithNullBio()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var updatedAt = DateTime.UtcNow;

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            Profile = new Profile { Bio = null }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Profile.Bio.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorHasNoWorks_ReturnsResponseWithEmptyWorksCollection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var bio = "Test bio";
        var updatedAt = DateTime.UtcNow;

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            Profile = new Profile { Bio = bio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.PublishedStories.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorIsDeleted_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var bio = "Test bio";
        var updatedAt = DateTime.UtcNow;
        var deletedAt = DateTime.UtcNow.AddDays(-1);

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
            Profile = new Profile { Bio = bio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WithCancellationToken_PassesCancellationTokenToDbContext()
    {
        // Arrange
        var nonExistentId = Ulid.NewUlid();
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _useCase.HandleAsync(nonExistentId, cancellationToken);

        // Assert - Just verify it doesn't throw and handles cancellation gracefully
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Short bio")]
    [InlineData("This is a much longer bio that contains multiple sentences and provides detailed information about the author's background, writing style, and interests.")]
    public async Task HandleAsync_WithVariousBioLengths_ReturnsCorrectBio(string bio)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var updatedAt = DateTime.UtcNow;

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            Profile = new Profile { Bio = bio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Profile.Bio.Should().Be(bio);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleWorks_ReturnsAllWorksInCorrectFormat()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var bio = "Test bio";
        var updatedAt = DateTime.UtcNow;

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            Profile = new Profile { Bio = bio }
        };

        var works = new List<Story>
        {
            new() { Title = "First Novel", Description = "A first novel", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow },
            new() { Title = "Second Novel", Description = "A second novel", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow },
            new() { Title = "Short Story Collection", Description = "A collection of short stories", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow },
            new() { Title = "Poetry Book", Description = "A book of poetry", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow },
            new() { Title = "Non-Fiction Work", Description = "A non-fiction work", Owner = author, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow }
        };

        foreach (var work in works)
        {
            author.Works.Add(work);
        }

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.PublishedStories.Should().HaveCount(5);

        var resultWorks = response.PublishedStories;
        var workTitles = resultWorks.Select(w => w.Title).ToList();
        workTitles.Should().Contain("First Novel");
        workTitles.Should().Contain("Second Novel");
        workTitles.Should().Contain("Short Story Collection");
        workTitles.Should().Contain("Poetry Book");
        workTitles.Should().Contain("Non-Fiction Work");

        // Verify all works have valid IDs
        resultWorks.Should().OnlyContain(w => w.Id != Ulid.Empty);
    }

    [Fact]
    public async Task HandleAsync_WithStoryAndChapters_ReturnsOnlyStoryNotChapters()
    {
        // Arrange - This test validates the fix for the issue where chapters were being counted as separate works
        var userId = Guid.NewGuid();
        var authorName = "Test Author";
        var bio = "Test bio";
        var updatedAt = DateTime.UtcNow;

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            UpdatedAt = updatedAt,
            Profile = new Profile { Bio = bio }
        };

        // Create a story with chapters
        var story = new Story
        {
            Title = "My Story",
            Description = "A story with chapters",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        };

        var chapter1 = new Chapter
        {
            Title = "Chapter 1",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            Story = story,
            StoryId = story.Id,
            PublishedAt = DateTime.UtcNow
        };

        var chapter2 = new Chapter
        {
            Title = "Chapter 2",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            Story = story,
            StoryId = story.Id,
            PublishedAt = DateTime.UtcNow
        };

        // Add story and chapters to author's works (simulating what the database would do)
        author.Works.Add(story);
        author.Works.Add(chapter1);
        author.Works.Add(chapter2);

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;

        // The response should only include the Story, not the Chapters
        response.PublishedStories.Should().HaveCount(1, "only the Story should be returned, not the Chapters");
        response.PublishedStories.Should().Contain(w => w.Title == "My Story");
        response.PublishedStories.Should().NotContain(w => w.Title == "Chapter 1");
        response.PublishedStories.Should().NotContain(w => w.Title == "Chapter 2");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(GetAuthorByIdTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(GetAuthorByIdTests)}"))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
                .WithDefaultInterceptors(sp.GetRequiredService<TimeProvider>())
                .Options));
    }

    public async ValueTask InitializeAsync()
    {
        if (_dbContext.Database.GetPendingMigrations().Any())
        {
            await _dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        // Close all connections before deleting database
        await _dbContext.Database.CloseConnectionAsync();

        // Use EnsureDeletedAsync for proper async database deletion
        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);

        await _dbContext.DisposeAsync();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}