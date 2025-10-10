using System.Globalization;
using System.Net.Mime;
using System.Xml.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;

using Sidio.Sitemap.Blazor;
using IHFiction.Data.Stories.Domain;

namespace IHFiction.SharedWeb.Extensions;

public static class SitemapExtensions
{
    public static IApplicationBuilder MapStaticSitemap(this IApplicationBuilder builder) => builder.UseSitemap();

    public static RouteHandlerBuilder MapDynamicSitemap(this IEndpointRouteBuilder builder) => builder.MapGet("/sitemap-dynamic.xml", async (HttpContext ctx, [FromServices] FictionDbContext db, TimeProvider dt) =>
    {
        var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host.Value}".TrimEnd('/');

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XElement urlset = new(ns + "urlset");

        void AddUrl(string path, DateTimeOffset? lastmod)
        {
            XElement url = new(ns + "url", new XElement(ns + "loc", $"{baseUrl}{path}"));

            if (lastmod is DateTimeOffset lm)
                url.Add(new XElement(ns + "lastmod", lm.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            urlset.Add(url);
        }

        // Authors List /authors
        AddUrl("/authors", dt.GetUtcNow());

        // Author Detail Pages /authors/{authorId}
        // Author Stories /authors/{authorId}/stories
        foreach (var author in db.Authors
            .Where(a => a.Works.Any(w => w is Story && w.PublishedAt != null))
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.UpdatedAt,
                Stories = a.Works
                    .Where(w => w is Story && w.PublishedAt != null)
                    .OrderByDescending(w => w.UpdatedAt)
                    .Take(1)
            }))
        {
            AddUrl($"/authors/{author.Id}", author.UpdatedAt);
            AddUrl($"/authors/{author.Id}/stories", author.Stories.First().UpdatedAt);
        }

        // Stories List /stories
        AddUrl("/stories", dt.GetUtcNow());

        // Story Detail /stories/{storyId}
        // Story Read /stories/{storyId}/read
        // Story Chapters /stories/{storyId}/chapters/{chapterId}
        foreach (var story in db.Stories
            .Include(s => s.Chapters)
            .AsNoTracking()
            .Where(s => s.PublishedAt != null)
            .Select(s => new { s.Id, s.UpdatedAt, Chapters = s.Chapters.Where(c => c.PublishedAt != null) }))
        {
            AddUrl($"/stories/{story.Id}", story.UpdatedAt);
            AddUrl($"/stories/{story.Id}/read", story.UpdatedAt);

            foreach (var chapter in story.Chapters)
            {
                AddUrl($"/stories/{story.Id}/chapters/{chapter.Id}", chapter.UpdatedAt);
            }
        }

        XDocument xml = new(new XDeclaration("1.0", "utf-8", "yes"), urlset);

        ctx.Response.ContentType = MediaTypeNames.Application.Xml;
        ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0, s-maxage=0";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";

        await ctx.Response.WriteAsync(xml.ToString(SaveOptions.DisableFormatting));
    });
}
