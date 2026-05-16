using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;

using Sidio.Sitemap.Blazor;
using Sidio.Sitemap.Core;

namespace IHFiction.SharedWeb.Sitemap;

public class DynamicSitemapNodeProvider(FictionDbContext db) : ICustomSitemapNodeProvider
{
    public IEnumerable<SitemapNode> GetNodes()
    {
        // Newest published author
        yield return new SitemapNode("/authors", db.Authors
            .Where(a => a.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .OrderBy(a => a.Id)
            .Last().UpdatedAt);

        // Newest published story
        yield return new SitemapNode("/stories", db.Stories
            .Where(s => s.PublishedAt != null)
            .OrderBy(s => s.Id)
            .Last().UpdatedAt);
            
        // Authors
        foreach (var author in db.Authors
            .Include(a => a.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .Where(a => a.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .AsNoTracking())
        {
            yield return new SitemapNode($"/authors/{author.Id}", author.UpdatedAt);

            yield return new SitemapNode($"/authors/{author.Id}/stories", author.Stories.Max(s => s.UpdatedAt));
        }

        // Stories and Chapters
        foreach (var story in db.Stories
            .Include(s => s.Chapters.Where(c => c.PublishedAt != null))
            .AsNoTracking()
            .Where(s => s.PublishedAt != null))
        {
            yield return new SitemapNode($"/stories/{story.Id}", story.UpdatedAt);

            foreach (var chapter in story.Chapters)
            {
                yield return new SitemapNode($"/stories/{story.Id}/chapters/{chapter.Id}", chapter.UpdatedAt);
            }
        }
    }
}
