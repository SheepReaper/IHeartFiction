using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;

using IHFiction.SharedKernel.Searching;

using Sidio.Sitemap.Blazor;
using Sidio.Sitemap.Core;

namespace IHFiction.SharedWeb.Sitemap;

public class DynamicSitemapNodeProvider(FictionDbContext db) : ICustomSitemapNodeProvider
{
    public IEnumerable<SitemapNode> GetNodes()
    {
        // Newest published author
        var newestAuthor = db.Authors
            .Where(a => a.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .OrderByDescending(a => a.Id)
            .FirstOrDefault();

        if (newestAuthor is not null)
        {
            yield return new SitemapNode("/authors", newestAuthor.UpdatedAt);
        }

        // Newest published story
        var newestStory = db.Stories
            .Where(s => s.PublishedAt != null)
            .OrderByDescending(s => s.Id)
            .FirstOrDefault();

        if (newestStory is not null)
        {
            yield return new SitemapNode("/stories", newestStory.UpdatedAt);
        }
            
        // Authors
        foreach (var author in db.Authors
            .Include(a => a.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .Where(a => a.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .AsNoTracking())
        {
            yield return new SitemapNode($"/authors/{author.Id}", author.UpdatedAt);

            yield return new SitemapNode($"/authors/{author.Id}/stories", author.Stories.Max(s => s.UpdatedAt));
        }

        // Active Canonical Tags
        var activeTags = db.Tags.OfType<CanonicalTag>()
            .Include(t => t.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .Include(t => t.Synonyms)
                .ThenInclude(s => s.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))
            .Where(t => t.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null)
                     || t.Synonyms.Any(s => s.Works.Any(w => w is Data.Stories.Domain.Story && w.PublishedAt != null)))
            .AsNoTracking()
            .ToList();

        if (activeTags.Count > 0)
        {
            var allTagWorks = activeTags
                .SelectMany(tag => tag.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null)
                    .Concat(tag.Synonyms.SelectMany(s => s.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null))))
                .ToList();

            if (allTagWorks.Count > 0)
            {
                yield return new SitemapNode("/tags", allTagWorks.Max(w => w.UpdatedAt));
            }

            foreach (var tag in activeTags)
            {
                var works = tag.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null)
                    .Concat(tag.Synonyms.SelectMany(s => s.Works.Where(w => w is Data.Stories.Domain.Story && w.PublishedAt != null)));

                var routeToken = TagRouteSpec.CreateRouteToken(tag.Id, tag.Category, tag.Subcategory, tag.Value);
                yield return new SitemapNode(TagRouteSpec.BuildPath(routeToken), works.Max(w => w.UpdatedAt));
            }
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
