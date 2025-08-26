using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Integration tests for GetAuthorById using in-memory database
/// These tests verify the complete flow from database to response
/// </summary>
// [Collection(nameof(AuthorsTestCollection))]
public class GetAuthorByIdIntegrationTests : BaseIntegrationTest, IConfigureServices<GetAuthorByIdIntegrationTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly GetAuthorById _useCase;

    public GetAuthorByIdIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(GetAuthorByIdIntegrationTests)) ?? throw new Exception("DbContext not found");
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
        ((dynamic)result!).UserId.Should().Be(userId);
        ((dynamic)result).Name.Should().Be(authorName);
        ((dynamic)result).UpdatedAt.Should().BeAfter(updatedAt.AddSeconds(-1));
        ((dynamic)result).DeletedAt.Should().BeNull();
        ((dynamic)result).Profile.Should().NotBeNull();
        ((dynamic)result).Profile.Bio.Should().Be(bio);
        ((dynamic)result).Works.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var nonExistentId = Ulid.NewUlid();

        // Act
        var result = await _useCase.HandleAsync(nonExistentId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorHasMultipleWorks_ReturnsAllWorks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Prolific Author";
        var bio = "An author with multiple works";

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            Profile = new Profile { Bio = bio }
        };

        // Create test works using concrete Story class
        var story = new Story
        {
            Title = "Test Story",
            Description = "A test story",
            Owner = author,
            UpdatedAt = DateTime.UtcNow
        };

        var book = new Book
        {
            Title = "Test Book",
            Description = "A test book",
            Owner = author,
            UpdatedAt = DateTime.UtcNow
        };

        // Add works to author
        author.Works.Add(story);
        author.Works.Add(book);

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        ((dynamic)result!).Works.Should().HaveCount(2);
        var works = (IEnumerable<GetAuthorById.AuthorWorkItem>)((dynamic)result).Works;
        works.Should().Contain(w => w.Title == "Test Story");
        works.Should().Contain(w => w.Title == "Test Book");
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorIsDeleted_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Deleted Author";
        var bio = "This author was deleted";
        var deletedAt = DateTime.UtcNow.AddDays(-1);

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            DeletedAt = deletedAt,
            Profile = new Profile { Bio = bio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull("deleted authors should not be returned by the API");
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorHasNoBio_ReturnsNullBio()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Author Without Bio";

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            Profile = new Profile { Bio = null }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        ((dynamic)result!).Profile.Bio.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorHasEmptyBio_ReturnsEmptyBio()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authorName = "Author With Empty Bio";

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            Profile = new Profile { Bio = "" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        ((dynamic)result!).Profile.Bio.Should().Be("");
    }

    [Fact]
    public async Task HandleAsync_WithSpecificUserId_ReturnsCorrectUserId()
    {
        // Arrange
        var targetUserId = new Guid("12345678-1234-1234-1234-123456789012");
        var authorName = "Author With Specific GUID";

        var author = new Author
        {
            UserId = targetUserId,
            Name = authorName,
            Profile = new Profile { Bio = "Test bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        ((dynamic)result!).UserId.Should().Be(targetUserId);
    }

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    [InlineData("87654321-4321-4321-4321-210987654321")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task HandleAsync_WithVariousUserIds_ReturnsCorrectUserId(string userIdString)
    {
        // Arrange
        var userId = Guid.Parse(userIdString);
        var authorName = $"Author {userId}";

        var author = new Author
        {
            UserId = userId,
            Name = authorName,
            Profile = new Profile { Bio = "Test bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        ((dynamic)result!).UserId.Should().Be(userId);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(GetAuthorByIdIntegrationTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(GetAuthorByIdIntegrationTests)}"))
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

    public override async ValueTask DisposeAsync()
    {
        // Close all connections before deleting database
        await _dbContext.Database.CloseConnectionAsync();

        // Use EnsureDeletedAsync for proper async database deletion
        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);

        await _dbContext.DisposeAsync();

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }


}