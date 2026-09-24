using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Stories;
using IHFiction.FictionApi.Tags;

using NSubstitute;

using Wolverine;

namespace IHFiction.IntegrationTests.Stories;

public sealed class TagTaxonomyIntegrationTests : BaseIntegrationTest, IConfigureServices<TagTaxonomyIntegrationTests>, IAsyncLifetime
{
    private readonly FictionDbContext _context;
    private bool _disposed;

    public TagTaxonomyIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _context = _scope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(TagTaxonomyIntegrationTests));
    }

    [Fact]
    public async Task ReplaceStoryTags_AddsAndRemovesTheCompleteSet()
    {
        var author = new Data.Authors.Domain.Author { Name = "Tag Author", UserId = Guid.NewGuid() };
        var story = new Story { Title = "Tagged Story", Description = "Test", Owner = author, OwnerId = author.Id };
        _context.AddRange(author, story);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new AddTagsToStory(_context, new UserService(_context), Substitute.For<IMessageBus>());
        var principal = Principal(author.UserId);

        var first = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody(["genre:Fantasy", "theme:Found Family"]),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value.TotalTags);

        var replacement = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody(["genre:Fantasy"]),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(replacement.IsSuccess);
        Assert.Single(replacement.Value.Tags);
        Assert.Single((await _context.Stories.Include(candidate => candidate.Tags)
            .SingleAsync(candidate => candidate.Id == story.Id, TestContext.Current.CancellationToken)).Tags);

        var empty = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody([]),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(empty.IsSuccess);
        Assert.Equal(0, empty.Value.TotalTags);
    }

    [Fact]
    public async Task RenameThenBulkMerge_PreservesSpellingsAndMovesWorkRelationships()
    {
        var source = Tag.CreateCanonical("genre", null, "Sci Fi");
        var secondSource = Tag.CreateCanonical("genre", null, "Space Opera");
        var target = Tag.CreateCanonical("genre", null, "Science Fiction");
        var author = new Data.Authors.Domain.Author { Name = "Taxonomy Admin", UserId = Guid.NewGuid() };
        var story = new Story { Title = "Space Story", Description = "Test", Owner = author, OwnerId = author.Id };
        var secondStory = new Story { Title = "Opera Story", Description = "Test", Owner = author, OwnerId = author.Id };
        story.Tags.Add(source);
        secondStory.Tags.Add(secondSource);
        _context.AddRange(author, source, secondSource, target, story, secondStory);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var principal = Principal(Guid.NewGuid());
        var rename = new RenameCanonicalTag(_context, NullLogger<RenameCanonicalTag>.Instance);
        var renamed = await rename.HandleAsync(
            source.Id,
            new RenameCanonicalTag.RenameCanonicalTagBody("genre", null, "Speculative Fiction", "Clarify display name"),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(renamed.IsSuccess);
        Assert.Contains("genre:Sci Fi", renamed.Value.Synonyms);

        var merge = new MergeCanonicalTags(_context, NullLogger<MergeCanonicalTags>.Instance);
        var merged = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                [source.Id.ToString(), secondSource.Id.ToString()],
                "Consolidate overlapping families"),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(merged.IsSuccess);
        Assert.Equal(2, merged.Value.MergedTagCount);

        _context.ChangeTracker.Clear();
        var persistedTarget = await _context.Tags.OfType<CanonicalTag>()
            .Include(tag => tag.Synonyms)
            .Include(tag => tag.Works)
            .SingleAsync(tag => tag.Id == target.Id, TestContext.Current.CancellationToken);
        Assert.Contains(persistedTarget.Synonyms, synonym => synonym.Value == "Speculative Fiction");
        Assert.Contains(persistedTarget.Synonyms, synonym => synonym.Value == "Sci Fi");
        Assert.Contains(persistedTarget.Synonyms, synonym => synonym.Value == "Space Opera");
        Assert.Contains(persistedTarget.Works, work => work.Id == story.Id);
        Assert.Contains(persistedTarget.Works, work => work.Id == secondStory.Id);
        Assert.False(await _context.Tags.OfType<CanonicalTag>().AnyAsync(
            tag => tag.Id == source.Id || tag.Id == secondSource.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BulkMerge_RejectsInvalidSelectionsBeforeChangingTaxonomy()
    {
        var source = Tag.CreateCanonical("genre", null, "Cyberpunk");
        var target = Tag.CreateCanonical("genre", null, "Science Fiction");
        _context.AddRange(source, target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var merge = new MergeCanonicalTags(_context, NullLogger<MergeCanonicalTags>.Instance);
        var principal = Principal(Guid.NewGuid());
        var duplicateSources = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                [source.Id.ToString(), source.Id.ToString()],
                "Duplicate selection"),
            principal,
            TestContext.Current.CancellationToken);
        var canonicalIncludedAsSource = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                [target.Id.ToString()],
                "Self merge"),
            principal,
            TestContext.Current.CancellationToken);
        var malformedSource = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                ["not-a-ulid"],
                "Malformed selection"),
            principal,
            TestContext.Current.CancellationToken);

        Assert.Equal(MergeCanonicalTags.Errors.InvalidSelection, duplicateSources.DomainError);
        Assert.Equal(MergeCanonicalTags.Errors.InvalidSelection, canonicalIncludedAsSource.DomainError);
        Assert.Equal(MergeCanonicalTags.Errors.InvalidSelection, malformedSource.DomainError);
        Assert.Equal(2, await _context.Tags.OfType<CanonicalTag>().CountAsync(TestContext.Current.CancellationToken));
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
        "Test"));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>.")]
    public static void ConfigureServices(IServiceCollection services) => services
        .AddKeyedTestFictionDbContext<TagTaxonomyIntegrationTests>(configurePendingModelWarning: false);

    public async ValueTask InitializeAsync()
    {
        await _context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;
        await _context.Database.CloseConnectionAsync();
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        _disposed = true;
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
