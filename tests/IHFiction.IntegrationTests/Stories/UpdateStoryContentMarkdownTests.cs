using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Markdown;

using MongoDB.Driver;

using NSubstitute;

namespace IHFiction.IntegrationTests.Stories;

/// <summary>
/// Integration tests for UpdateStoryContent with markdown validation
/// Tests the complete flow from use case to database with markdown content
/// </summary>
// [Collection(nameof(StoriesTestCollection))]
public sealed class UpdateStoryContentMarkdownTests : BaseIntegrationTest, IConfigureServices<UpdateStoryContentMarkdownTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly IMongoCollection<WorkBody> _workBodies;
    private readonly UserService _userService;
    private readonly TimeProvider _dateTimeProvider;
    private readonly IOptions<MarkdownOptions> _markdownOptions;
    private readonly IHostEnvironment _environment;
    private readonly UpdateStoryContent _useCase;
    private bool _disposed;

    public UpdateStoryContentMarkdownTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(UpdateStoryContentMarkdownTests)) ?? throw new Exception("DbContext not found");
        _workBodies = _scope.ServiceProvider.GetKeyedService<IMongoCollection<WorkBody>>(nameof(UpdateStoryContentMarkdownTests)) ?? throw new Exception("WorkBodies collection not found");

        // Create mock dependencies
        _userService = new UserService(_dbContext); // Use real UserService with test context
        _dateTimeProvider = Substitute.For<TimeProvider>();
        _markdownOptions = Substitute.For<IOptions<MarkdownOptions>>();
        _environment = Substitute.For<IHostEnvironment>();

        // Setup mock behavior
        _dateTimeProvider.GetUtcNow().Returns(new DateTimeOffset(DateTime.UtcNow));

        var markdownOptions = new MarkdownOptions();
        markdownOptions.AllowedImageDomains.Add("imgur.com");
        markdownOptions.AllowedImageDomains.Add("example.com");
        markdownOptions.MaxBase64ImageSizeBytes = 5 * 1024 * 1024; // 5MB
        _markdownOptions.Value.Returns(markdownOptions);

        _environment.EnvironmentName.Returns("Development");

        _useCase = new UpdateStoryContent(
            _dbContext,
            _workBodies,
            _userService,
            _dateTimeProvider,
            _markdownOptions,
            _environment);
    }

    [Fact]
    public async Task UpdateStoryContent_WithValidMarkdown_ReturnsSuccess()
    {
        // Arrange
        var (storyId, authorId, claimsPrincipal) = await CreateTestStoryAsync();

        var request = new UpdateStoryContent.UpdateStoryContentBody(
            Content: """
                # Chapter 1: The Beginning

                This is a story with **bold** and *italic* text.

                ![Character portrait](https://imgur.com/character.png "Main Character")

                The character thought:

                > "This is going to be an adventure!"

                For more information, visit [the author's website](https://example.com).

                ## Technical Details

                ```csharp
                public class Character
                {
                    public string Name { get; set; }
                }
                ```
                """,
            Note1: "This is a **markdown** note.",
            Note2: "Another *formatted* note.");

        // Act
        var result = await _useCase.HandleAsync(storyId, request, claimsPrincipal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(storyId, ((dynamic)result.Value!).StoryId);
    }

    [Fact]
    public async Task UpdateStoryContent_WithValidBase64Image_ReturnsSuccess()
    {
        // Arrange
        var (storyId, authorId, claimsPrincipal) = await CreateTestStoryAsync();

        // Small valid base64 PNG (1x1 transparent pixel)
        var request = new UpdateStoryContent.UpdateStoryContentBody(
            Content: "![Embedded image](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==)");

        // Act
        var result = await _useCase.HandleAsync(storyId, request, claimsPrincipal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateStoryContent_WithComplexValidMarkdown_ReturnsSuccess()
    {
        // Arrange
        var (storyId, authorId, claimsPrincipal) = await CreateTestStoryAsync();

        var request = new UpdateStoryContent.UpdateStoryContentBody(
            Content: """
                # The Epic Tale

                ## Chapter 1: Introduction

                Once upon a time, there was a **brave** hero who embarked on an *incredible* journey.

                ### The Hero's Equipment

                - Sword of Truth
                - Shield of Valor
                - Boots of Speed

                ### The Quest

                1. Find the ancient artifact
                2. Defeat the dragon
                3. Save the kingdom

                ![The hero's portrait](https://imgur.com/hero.png "Our brave protagonist")

                The wise sage said:

                > "Only those pure of heart can wield the Sword of Truth."

                For more lore, visit the [Kingdom's Archive](https://example.com/lore).

                #### Code of Honor

                The hero lived by this code:

                ```
                1. Protect the innocent
                2. Speak only truth
                3. Show mercy to enemies
                ```

                ---

                *To be continued...*
                """,
            Note1: "**Author's Note:** This is the first chapter of my epic fantasy series.",
            Note2: "*Editor's Note:* Great start! Looking forward to the next chapter.");

        // Act
        var result = await _useCase.HandleAsync(storyId, request, claimsPrincipal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(storyId, ((dynamic)result.Value!).StoryId);
    }

    private async Task<(Ulid storyId, Ulid authorId, ClaimsPrincipal claimsPrincipal)> CreateTestStoryAsync()
    {
        var userId = Guid.NewGuid();
        var author = new Author
        {
            Name = "Test Author",
            UserId = userId
        };

        var story = new Story
        {
            Title = "Test Story",
            Description = "A test story for markdown testing",
            Owner = author,
            OwnerId = author.Id
        };

        _dbContext.Authors.Add(author);
        _dbContext.Stories.Add(story);
        await _dbContext.SaveChangesAsync();

        // Create ClaimsPrincipal for the test user
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }));

        return (story.Id, author.Id, claimsPrincipal);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(UpdateStoryContentMarkdownTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(UpdateStoryContentMarkdownTests)}"))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
                .WithDefaultInterceptors(sp.GetRequiredService<TimeProvider>())
                .Options));

        services.AddKeyedScoped(
            nameof(UpdateStoryContentMarkdownTests),
            (sp, key) => sp.GetRequiredService<IMongoClient>()
                .GetDatabase($"test_stories_{nameof(UpdateStoryContentMarkdownTests)}")
                .GetCollection<WorkBody>("works"));
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
        if (_disposed) return;

        // Close all connections before deleting databases
        await _dbContext.Database.CloseConnectionAsync();

        // Use EnsureDeletedAsync for proper async database deletion
        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _workBodies.Database.Client.DropDatabaseAsync(_workBodies.Database.DatabaseNamespace.DatabaseName, TestContext.Current.CancellationToken);

        await _dbContext.DisposeAsync();

        _disposed = true;

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}