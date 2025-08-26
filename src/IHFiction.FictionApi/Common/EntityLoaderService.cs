using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Centralized service for loading entities with consistent include strategies.
/// Eliminates duplicate database query patterns and provides optimized data loading.
/// </summary>
internal sealed class EntityLoaderService(FictionDbContext context)
{
    /// <summary>
    /// Loads a story with its related author and collaboration data.
    /// Standardizes the story loading pattern used across multiple endpoints.
    /// </summary>
    public async Task<Story?> LoadStoryWithAuthorsAsync(
        Ulid storyId, 
        bool includeDeleted = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }

    /// <summary>
    /// Loads a story with comprehensive related data including tags and chapters.
    /// Used for endpoints that need full story context.
    /// </summary>
    public async Task<Story?> LoadStoryWithFullDetailsAsync(
        Ulid storyId,
        bool includeDeleted = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .Include(s => s.Tags)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }

    public async Task<Story?> LoadStoryForConversionAsync(Ulid storyId, CancellationToken cancellationToken)
    {
        return await context.Stories
            .Include(s => s.Chapters)
            .Include(s => s.Books)
                .ThenInclude(b => b.Chapters)
            .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }
}
