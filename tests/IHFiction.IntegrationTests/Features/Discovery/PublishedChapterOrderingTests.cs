using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;

namespace IHFiction.IntegrationTests.Features.Discovery;

public sealed class PublishedChapterOrderingTests(IntegrationTestWebAppFactory factory) : IConfigureServices<PublishedChapterOrderingTests>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChapterList_UsesNarrativeOrderAndStableIdTies(bool tiedPositions)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(PublishedChapterOrderingTests));
        await context.Database.MigrateAsync(cancellationToken);
        var author = new Author { Name = "Chapter Author", UserId = Guid.NewGuid(), Profile = new Profile { Bio = "Test" } };
        var story = new Story { Title = "Chapter ordering", Description = "Test", Owner = author, OwnerId = author.Id, PublishedAt = DateTime.UtcNow };
        story.Authors.Add(author);
        var other = new Story { Title = "Other story", Description = "Test", Owner = author, OwnerId = author.Id, PublishedAt = DateTime.UtcNow };
        var draft = new Story { Title = "Draft story", Description = "Test", Owner = author, OwnerId = author.Id };
        var first = Chapter(story, 0, DateTime.UtcNow.AddDays(-1));
        var second = Chapter(story, tiedPositions ? 0 : 1, DateTime.UtcNow.AddDays(-2));
        // Assign IDs explicitly so ties cannot follow insertion or publication order.
        var ids = new[] { Ulid.NewUlid(), Ulid.NewUlid() }.Order().ToArray();
        first.Id = ids[0];
        second.Id = ids[1];
        context.AddRange(second, first, Chapter(story, 0, null), Chapter(other, 0, DateTime.UtcNow), Chapter(draft, 0, DateTime.UtcNow));
        await context.SaveChangesAsync(cancellationToken);

        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped(provider => provider.GetRequiredKeyedService<FictionDbContext>(nameof(PublishedChapterOrderingTests)))));
        using var client = app.CreateClient();
        var path = $"/stories/{story.Id}/chapters";
        using var list = await ReadAsync(path);
        var items = list.RootElement.GetProperty("data").EnumerateArray().ToArray();
        var expectedIds = new[] { first.Id.ToString(), second.Id.ToString() };
        Assert.Equal(expectedIds, items.Select(item => item.GetProperty("chapterId").GetString()));
        Assert.Equal(2, list.RootElement.GetProperty("totalCount").GetInt32());
        foreach (var item in items)
        {
            var itemLinks = item.GetProperty("links").EnumerateArray().ToArray();
            Assert.Equal(new[] { "content", "self", "story" }, itemLinks.Select(l => l.GetProperty("rel").GetString()).Order());
            var link = Assert.Single(itemLinks, l => l.GetProperty("rel").GetString() == "content");
            Assert.EndsWith($"/chapters/{item.GetProperty("chapterId").GetString()}/content", link.GetProperty("href").GetString());
        }
        using var shaped = await ReadAsync(path + "?Fields=chapterId,title");
        Assert.Equal(expectedIds, shaped.RootElement.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("chapterId").GetString()));
        Assert.All(shaped.RootElement.GetProperty("data").EnumerateArray(), item => Assert.False(item.TryGetProperty("publishedAt", out _)));
        using var repeated = await ReadAsync(path);
        Assert.Equal(expectedIds, repeated.RootElement.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("chapterId").GetString()));
        if (!tiedPositions)
        {
            using var detail = await ReadAsync($"/stories/{story.Id}");
            Assert.Equal(expectedIds, detail.RootElement.GetProperty("chapters").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
            using var meta = await ReadAsync($"/works/{story.Id}/meta");
            Assert.Equal(expectedIds, meta.RootElement.GetProperty("readableChildren").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        }
        foreach (var (id, code) in new[] { (draft.Id, "ListStoryChapters.StoryNotPublished"), (Ulid.NewUlid(), "ListStoryChapters.StoryNotFound") })
        {
            using var response = await client.GetAsync($"/stories/{id}/chapters", cancellationToken);
            Assert.False(response.IsSuccessStatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            Assert.Equal(code, problem.RootElement.GetProperty("domainError").GetProperty("code").GetString());
        }

        async Task<JsonDocument> ReadAsync(string url)
        {
            using var response = await client.GetAsync(url, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private static Chapter Chapter(Story story, int order, DateTime? publishedAt)
        => new() { Story = story, StoryId = story.Id, Owner = story.Owner, OwnerId = story.OwnerId,
            Title = $"Chapter {order}", Order = order, PublishedAt = publishedAt };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "Implements IConfigureServices<T>.")]
    public static void ConfigureServices(IServiceCollection services)
        => services.AddKeyedTestFictionDbContext<PublishedChapterOrderingTests>(configurePendingModelWarning: false);
}
