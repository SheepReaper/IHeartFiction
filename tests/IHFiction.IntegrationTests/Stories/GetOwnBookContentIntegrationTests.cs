using System.Security.Claims;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Account;
using IHFiction.FictionApi.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

namespace IHFiction.IntegrationTests.Stories;

public sealed class GetOwnBookContentIntegrationTests : BaseIntegrationTest, IConfigureServices<GetOwnBookContentIntegrationTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly IMongoCollection<WorkBody> _workBodies;
    private readonly GetOwnBookContent _useCase;
    private bool _disposed;

    public GetOwnBookContentIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(GetOwnBookContentIntegrationTests))
            ?? throw new Exception("DbContext not found");

        _workBodies = _scope.ServiceProvider.GetKeyedService<IMongoCollection<WorkBody>>(nameof(GetOwnBookContentIntegrationTests))
            ?? throw new Exception("WorkBodies collection not found");

        var userService = new UserService(_dbContext);
        var authorizationService = new AuthorizationService(_dbContext, userService);

        _useCase = new GetOwnBookContent(_workBodies, _dbContext, authorizationService);
    }

    [Fact]
    public async Task HandleAsync_AllowsOwner_WhenBookAuthorsRelationIsMissing()
    {
        var owner = new Author { Name = "Owner", UserId = Guid.NewGuid() };

        var story = new Story
        {
            Title = "Story A",
            Description = "Story A description",
            Owner = owner,
            OwnerId = owner.Id
        };

        var book = new Book
        {
            Title = "Book 1",
            Description = "Book 1 description",
            Owner = owner,
            OwnerId = owner.Id,
            Story = story,
            StoryId = story.Id,
            Order = 0
        };

        // Intentionally do not add owner to book.Authors to mimic legacy data.
        _dbContext.Authors.Add(owner);
        _dbContext.Stories.Add(story);
        _dbContext.Books.Add(book);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var claimsPrincipal = BuildClaimsPrincipal(owner);

        var result = await _useCase.HandleAsync(book.Id, claimsPrincipal, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsFailure ? result.DomainError.Description : "Expected success");
        Assert.Equal(book.Id, result.Value.Id);
        Assert.Equal(book.Title, result.Value.Title);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotAuthorized_ForUnrelatedAuthor()
    {
        var owner = new Author { Name = "Owner", UserId = Guid.NewGuid() };
        var intruder = new Author { Name = "Intruder", UserId = Guid.NewGuid() };

        var story = new Story
        {
            Title = "Story B",
            Description = "Story B description",
            Owner = owner,
            OwnerId = owner.Id
        };

        var book = new Book
        {
            Title = "Book 1",
            Description = "Book 1 description",
            Owner = owner,
            OwnerId = owner.Id,
            Story = story,
            StoryId = story.Id,
            Order = 0
        };

        _dbContext.Authors.AddRange(owner, intruder);
        _dbContext.Stories.Add(story);
        _dbContext.Books.Add(book);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var claimsPrincipal = BuildClaimsPrincipal(intruder);

        var result = await _useCase.HandleAsync(book.Id, claimsPrincipal, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(CommonErrors.Book.NotAuthorized, result.DomainError);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddKeyedTestFictionDbContext<GetOwnBookContentIntegrationTests>(configurePendingModelWarning: false, useDefaultInterceptors: false)
            .AddKeyedTestWorkBodyCollection<GetOwnBookContentIntegrationTests>();
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

        await _dbContext.Database.CloseConnectionAsync();
        await _dbContext.Database.EnsureDeletedAsync();
        await _workBodies.Database.Client.DropDatabaseAsync(_workBodies.Database.DatabaseNamespace.DatabaseName);

        await _dbContext.DisposeAsync();

        _disposed = true;

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(Author author) =>
        new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, author.UserId.ToString())
        ]));
}