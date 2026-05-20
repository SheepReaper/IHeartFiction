using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Centralized service for loading entities with consistent include strategies.
/// Eliminates duplicate database query patterns and provides optimized data loading.
/// 
/// IMPORTANT: Each loader is carefully optimized for its specific use cases. 
/// Picking the wrong loader will cause N+1 queries. See documentation below.
/// </summary>
internal sealed class EntityLoaderService(FictionDbContext context)
{
    /// <summary>
    /// Loads a story with ONLY essential metadata (Owner).
    /// 
    /// Includes:
    ///   - Owner
    /// 
    /// Use when: Authorization checks, publication status, WorkBodyId lookup.
    /// 
    /// DO NOT USE if you need: Authors, Tags, Chapters, Books, Cover, social links.
    /// </summary>
    public async Task<Story?> LoadStoryMetadataOnlyAsync(
        Ulid storyId,
        bool includeDeleted = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Story> query = context.Stories;

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (asNoTracking)
            query = query.AsNoTracking();

        query = query.Include(s => s.Owner);

        return await query.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }

    /// <summary>
    /// Loads a story with Author and Co-Author data (NO Profile/SocialLinks).
    /// 
    /// Includes:
    ///   - Owner
    ///   - Authors (without Profile/SocialLinks)
    /// 
    /// Use when: Authorization checks, listing collaborators WITHOUT needing their social links.
    /// 
    /// WARNING: If you access author.Profile.SocialLinks, you'll trigger N+1 queries.
    /// Use LoadStoryWithFullDetailsAsync if you need author social links.
    /// </summary>
    public async Task<Story?> LoadStoryWithAuthorsAsync(
        Ulid storyId, 
        bool includeDeleted = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Story> query = context.Stories;

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (asNoTracking)
            query = query.AsNoTracking();

        query = query
            .Include(s => s.Owner)
            .Include(s => s.Authors);

        return await query.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }

    /// <summary>
    /// Loads a story with comprehensive related data.
    /// 
    /// Includes:
    ///   - Owner
    ///   - Authors (WITH Profile.SocialLinks)
    ///   - Cover
    ///   - Tags
    ///   - Chapters
    ///   - Books and their Chapters
    /// 
    /// Use when: Full story display, story detail pages, anywhere you need complete metadata + relationships.
    /// This is the most expensive loader. Use it only when you need all the data.
    /// </summary>
    public async Task<Story?> LoadStoryWithFullDetailsAsync(
        Ulid storyId,
        bool includeDeleted = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Story> query = context.Stories;

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        if (asNoTracking)
            query = query.AsNoTracking();

        query = query
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .ThenInclude(a => a.Profile)
            .ThenInclude(p => p.SocialLinks)
            .Include(s => s.Cover)
            .Include(s => s.Tags)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .ThenInclude(b => b.Chapters);

        return await query.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }

    /// <summary>
    /// Loads a story with data needed for type conversions and structure operations.
    /// 
    /// Includes:
    ///   - Owner
    ///   - Authors
    ///   - Cover
    ///   - Chapters
    ///   - Books and their Chapters
    /// 
    /// Use when: Story type conversions, operations that modify structure (chapters, books).
    /// Does NOT include: Tags, Author SocialLinks (use LoadStoryWithFullDetailsAsync if needed).
    /// </summary>
    public async Task<Story?> LoadStoryForConversionAsync(Ulid storyId, CancellationToken cancellationToken)
    {
        return await context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .Include(s => s.Cover)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
                .ThenInclude(b => b.Chapters)
            .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }
}
