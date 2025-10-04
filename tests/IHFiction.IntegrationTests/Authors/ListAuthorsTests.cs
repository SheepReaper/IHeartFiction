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
/// Integration tests for ListAuthors functionality
/// Tests the complete use case handler with database to ensure story counts are correct
/// </summary>
public class ListAuthorsTests : BaseIntegrationTest, IConfigureServices<ListAuthorsTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly IPaginationService _paginationService;
    private readonly ListAuthors _useCase;

    public ListAuthorsTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(ListAuthorsTests)) ?? throw new Exception("DbContext not found");
        _paginationService = _scope.ServiceProvider.GetRequiredService<IPaginationService>();
        _useCase = new ListAuthors(_dbContext, _paginationService);
    }

    [Fact]
    public async Task HandleAsync_WithStoryAndChapters_CountsOnlyStoriesNotChapters()
    {
        // Arrange - This test validates the fix for the issue where chapters were being counted as separate stories
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

        // Create a published story with published chapters
        var story = new Story
        {
            Title = "My Published Story",
            Description = "A story with chapters",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow.AddDays(-1)
        };

        var chapter1 = new Chapter
        {
            Title = "Chapter 1",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            Story = story,
            StoryId = story.Id,
            PublishedAt = DateTime.UtcNow.AddDays(-1)
        };

        var chapter2 = new Chapter
        {
            Title = "Chapter 2",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            Story = story,
            StoryId = story.Id,
            PublishedAt = DateTime.UtcNow.AddDays(-1)
        };

        // Add story and chapters to author's owned works (simulating what the database would do)
        author.OwnedWorks.Add(story);
        author.OwnedWorks.Add(chapter1);
        author.OwnedWorks.Add(chapter2);

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var query = new ListAuthors.Query();
        var result = await _useCase.HandleAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;

        // Should have 1 author
        response.Data.Should().HaveCount(1);

        var authorItem = response.Data.First();
        authorItem.Name.Should().Be(authorName);

        // The counts should only include the Story, not the Chapters
        authorItem.TotalStories.Should().Be(1, "only the Story should be counted, not the Chapters");
        authorItem.PublishedStories.Should().Be(1, "only the published Story should be counted, not the published Chapters");
    }

    [Fact]
    public async Task HandleAsync_WithUnpublishedStoryAndPublishedChapters_DoesNotIncludeAuthor()
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

        // Create an unpublished story with published chapters
        var story = new Story
        {
            Title = "My Unpublished Story",
            Description = "A story with chapters",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = null // Not published
        };

        var chapter1 = new Chapter
        {
            Title = "Chapter 1",
            Owner = author,
            UpdatedAt = DateTime.UtcNow,
            Story = story,
            StoryId = story.Id,
            PublishedAt = DateTime.UtcNow.AddDays(-1) // Published, but parent story is not
        };

        author.OwnedWorks.Add(story);
        author.OwnedWorks.Add(chapter1);

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var query = new ListAuthors.Query();
        var result = await _useCase.HandleAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;

        // Author should not be included because they don't have any published Stories
        // (having published Chapters doesn't count)
        response.Data.Should().BeEmpty("author should not be included without a published Story");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(ListAuthorsTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(ListAuthorsTests)}"))
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
