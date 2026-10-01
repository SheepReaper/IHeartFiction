using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.SharedWeb.Sitemap;

namespace IHFiction.UnitTests.Sitemap;

public sealed class DynamicSitemapNodeProviderTests
{
    [Fact]
    public async Task GetNodes_IncludesActiveCanonicalTagsWithPublishedStories()
    {
        await using var context = CreateContext();
        var author = new Author { Id = Ulid.NewUlid(), UserId = Guid.NewGuid(), Name = "Sitemap Author" };
        var activeTag = Tag.CreateCanonical("genre", null, "Fantasy");
        var activeSynonymParent = Tag.CreateCanonical("universe", null, "Kantai Collection");
        var activeSynonym = Tag.CreateSynonym(activeSynonymParent, "universe", null, "Kancolle");
        var inactiveTag = Tag.CreateCanonical("genre", null, "Horror");

        var story1 = new Story
        {
            Title = "Fantasy Adventure",
            Description = "Published story",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        story1.Tags.Add(activeTag);

        var story2 = new Story
        {
            Title = "Fleet Adventure",
            Description = "Story with synonym tag",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        story2.Tags.Add(activeSynonym);

        context.AddRange(author, activeTag, activeSynonymParent, activeSynonym, inactiveTag, story1, story2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new DynamicSitemapNodeProvider(context);
        var nodes = provider.GetNodes().ToList();

        Assert.Contains(nodes, n => n.Url == "/tags");
        Assert.Contains(nodes, n => n.Url == "/tags/genre:fantasy");
        Assert.Contains(nodes, n => n.Url == "/tags/universe:kantai-collection");
        Assert.DoesNotContain(nodes, n => n.Url == "/tags/genre:horror");
    }

    private static FictionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FictionDbContext(options);
    }
}
