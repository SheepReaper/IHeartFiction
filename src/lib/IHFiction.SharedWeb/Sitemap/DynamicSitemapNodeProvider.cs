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
            .Include(s => s.Books.Where(b => b.PublishedAt != null))
                .ThenInclude(b => b.Chapters.Where(c => c.PublishedAt != null))
            .AsNoTracking()
            .Where(s => s.PublishedAt != null))
        {
            yield return new SitemapNode($"/stories/{story.Id}", story.UpdatedAt);

            if (!story.HasChapters && !story.HasBooks && story.HasContent)
            {
                yield return new SitemapNode($"/read/{story.Id}", story.UpdatedAt);
            }

            foreach (var chapter in story.Chapters.Where(c => c.PublishedAt != null))
            {
                yield return new SitemapNode($"/read/{chapter.Id}", chapter.UpdatedAt);
            }

            foreach (var chapter in story.Books
                .Where(b => b.PublishedAt != null)
                .SelectMany(b => b.Chapters.Where(c => c.PublishedAt != null)))
            {
                yield return new SitemapNode($"/read/{chapter.Id}", chapter.UpdatedAt);
            }
        }
    }
}
