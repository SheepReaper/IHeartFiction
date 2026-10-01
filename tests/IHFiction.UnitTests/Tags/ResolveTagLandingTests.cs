using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Tags;

namespace IHFiction.UnitTests.Tags;

public sealed class ResolveTagLandingTests
{
    [Fact]
    public async Task HandleAsync_SynonymAndFormattingAlias_ReturnsCanonicalRouteAndFamily()
    {
        await using var context = CreateContext();
        var canonical = Tag.CreateCanonical("Universe", null, "Kantai Collection");
        var synonym = Tag.CreateSynonym(canonical, "universe", null, "Kancolle");
        context.AddRange(canonical, synonym);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new ResolveTagLanding(context).HandleAsync(
            "UNIVERSE:kancolle",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("universe:kantai-collection", result.Value.CanonicalSpec);
        var tag = Assert.Single(result.Value.Tags);
        Assert.Equal(canonical.Id, tag.TagId);
        Assert.Contains(canonical.Id, tag.FamilyTagIds);
        Assert.Contains(synonym.Id, tag.FamilyTagIds);
    }

    [Fact]
    public async Task HandleAsync_AmbiguousValueOnlyTag_ReturnsConflict()
    {
        await using var context = CreateContext();
        context.AddRange(
            Tag.CreateCanonical("genre", null, "Drama"),
            Tag.CreateCanonical("trope", null, "Drama"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new ResolveTagLanding(context).HandleAsync(
            "drama",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResolveTagLanding.Errors.Conflict, result.DomainError);
    }

    [Fact]
    public async Task HandleAsync_StoryAttachedToSynonym_IsIncludedInCanonicalStoryCount()
    {
        await using var context = CreateContext();
        var canonical = Tag.CreateCanonical("universe", null, "Kantai Collection");
        var synonym = Tag.CreateSynonym(canonical, "universe", null, "Kancolle");
        var author = new Author { Id = Ulid.NewUlid(), UserId = Guid.NewGuid(), Name = "Author" };
        var story = new Story
        {
            Title = "Synonym Story",
            Description = "Published",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        story.Tags.Add(synonym);
        context.AddRange(canonical, synonym, author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new ResolveTagLanding(context).HandleAsync(
            "universe:kantai-collection",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.StoryCount);
    }

    [Fact]
    public async Task HandleAsync_IdRoute_ResolvesNonAsciiTag()
    {
        await using var context = CreateContext();
        var tag = Tag.CreateCanonical("fandom", null, "Pokémon");
        context.Add(tag);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new ResolveTagLanding(context).HandleAsync(
            $"id:{tag.Id}",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal($"id:{tag.Id.ToString().ToLowerInvariant()}", result.Value.CanonicalSpec);
    }

    private static FictionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FictionDbContext(options);
    }
}
