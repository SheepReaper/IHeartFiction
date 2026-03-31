using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;

using Sidio.Sitemap.Blazor;
using Sidio.Sitemap.Core;

namespace IHFiction.SharedWeb.Sitemap;

public class DynamicSitemapNodeProvider(FictionDbContext db) : ICustomSitemapNodeProvider
{
    private readonly FictionDbContext _db = db;

    public IEnumerable<SitemapNode> GetNodes()
    {
        // Authors
        foreach (var author in _db.Authors
            .Include(a => a.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .Where(a => a.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .AsNoTracking())
        {
            yield return new SitemapNode($"/authors/{author.Id}", author.UpdatedAt);

            yield return new SitemapNode($"/authors/{author.Id}/stories", author.Stories.Max(s => s.UpdatedAt));
        }

        // Stories and Chapters
        foreach (var story in _db.Stories
            .Include(s => s.Chapters.Where(c => c.PublishedAt != null))
            .AsNoTracking()
            .Where(s => s.PublishedAt != null))
        {
            yield return new SitemapNode($"/stories/{story.Id}", story.UpdatedAt);
            yield return new SitemapNode($"/stories/{story.Id}/read", story.UpdatedAt);

            foreach (var chapter in story.Chapters)
            {
                yield return new SitemapNode($"/stories/{story.Id}/chapters/{chapter.Id}", chapter.UpdatedAt);
            }
        }
    }
}
