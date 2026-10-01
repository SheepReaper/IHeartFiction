using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Searching;

namespace IHFiction.FictionApi.Tags;

internal sealed class ResolveTagLanding(FictionDbContext context) : IUseCase, INameEndpoint<ResolveTagLanding>
{
    internal static class Errors
    {
        public static readonly DomainError InvalidSpec = new(
            "ResolveTagLanding.InvalidSpec",
            "Spec must contain one to three tags separated by commas. Each tag must use category:value, category:subcategory:value, a unique value, or id:tagId.");

        public static readonly DomainError NotFound = new(
            "ResolveTagLanding.NotFound",
            "One or more requested tags do not exist.");

        public static readonly DomainError Conflict = new(
            "ResolveTagLanding.Conflict",
            "A value-only tag matches more than one canonical tag. Use a fully qualified category:value tag.");
    }

    internal sealed record ResolveTagLandingQuery(
        [property: FromQuery(Name = "spec")]
        [property: Required]
        [property: StringLength(TagRouteSpec.MaxLength)]
        string Spec);

    internal sealed record ResolvedTagItem(
        Ulid TagId,
        string Category,
        string? Subcategory,
        string Value,
        string NormalizedKey,
        string RouteKey,
        IReadOnlyList<Ulid> FamilyTagIds);

    internal sealed record ResolveTagLandingResponse(
        string CanonicalSpec,
        IReadOnlyList<ResolvedTagItem> Tags,
        int StoryCount);

    public async Task<Result<ResolveTagLandingResponse>> HandleAsync(
        string spec,
        bool includeStoryCount = true,
        CancellationToken cancellationToken = default)
    {
        if (!TagRouteSpec.TryParse(spec, out var tokens, out _)) return Errors.InvalidSpec;

        var requestedIds = tokens.Where(token => token.TagId is not null).Select(token => token.TagId!.Value).ToArray();
        var normalizedKeys = tokens.Where(token => token.NormalizedKey is not null).Select(token => token.NormalizedKey!).ToArray();
        var normalizedValues = tokens.Where(token => !token.IsFullyQualified).Select(token => token.NormalizedValue).ToArray();

        var candidates = await context.Tags
            .AsNoTracking()
            .Where(tag => requestedIds.Contains(tag.Id)
                || normalizedKeys.Contains(tag.NormalizedKey)
                || (normalizedValues.Length > 0 && (tag.NormalizedKey == normalizedValues[0] || tag.NormalizedKey.EndsWith(":" + normalizedValues[0])))
                || (normalizedValues.Length > 1 && (tag.NormalizedKey == normalizedValues[1] || tag.NormalizedKey.EndsWith(":" + normalizedValues[1])))
                || (normalizedValues.Length > 2 && (tag.NormalizedKey == normalizedValues[2] || tag.NormalizedKey.EndsWith(":" + normalizedValues[2]))))
            .Select(tag => new CandidateTag(
                tag.Id,
                tag is SynonymTag ? ((SynonymTag)tag).CanonicalTagId : tag.Id,
                tag.NormalizedKey))
            .ToListAsync(cancellationToken);

        var canonicalIds = new List<Ulid>(tokens.Count);
        foreach (var token in tokens)
        {
            var matches = candidates
                .Where(candidate => Matches(token, candidate))
                .Select(candidate => candidate.CanonicalTagId)
                .Distinct()
                .ToArray();

            if (matches.Length == 0) return Errors.NotFound;
            if (matches.Length > 1) return Errors.Conflict;
            canonicalIds.Add(matches[0]);
        }

        canonicalIds = [.. canonicalIds.Distinct()];
        var canonicalEntities = await context.Tags
            .AsNoTracking()
            .OfType<CanonicalTag>()
            .Where(tag => canonicalIds.Contains(tag.Id))
            .Include(tag => tag.Synonyms)
            .ToListAsync(cancellationToken);
        var canonicalTags = canonicalEntities
            .Select(tag => new ResolvedTagItem(
                tag.Id,
                tag.Category,
                tag.Subcategory,
                tag.Value,
                tag.NormalizedKey,
                TagRouteSpec.CreateRouteToken(tag.Id, tag.Category, tag.Subcategory, tag.Value),
                [.. tag.Synonyms.Select(synonym => synonym.Id), tag.Id]))
            .ToList();

        if (canonicalTags.Count != canonicalIds.Count) return Errors.NotFound;

        var orderedTags = canonicalTags.OrderBy(tag => tag.RouteKey, StringComparer.Ordinal).ToArray();
        var storyCount = 0;
        if (includeStoryCount)
        {
            var stories = context.Stories.AsNoTracking().Where(story => story.PublishedAt != null);
            stories = orderedTags
                .Select(tag => tag.FamilyTagIds)
                .Aggregate(stories, (current, familyIds) =>
                    current.Where(story => story.Tags.Any(storyTag => familyIds.Contains(storyTag.Id))));

            storyCount = await stories.CountAsync(cancellationToken);
        }

        return new ResolveTagLandingResponse(
            TagRouteSpec.BuildCanonicalSpec(orderedTags.Select(tag => tag.RouteKey)),
            orderedTags,
            storyCount);
    }

    private static bool MatchesValue(string normalizedKey, string normalizedValue) =>
        normalizedKey == normalizedValue || normalizedKey.EndsWith(":" + normalizedValue, StringComparison.Ordinal);

    private static bool Matches(TagRouteToken token, CandidateTag candidate)
    {
        if (token.TagId is not null) return candidate.TagId == token.TagId.Value;
        if (token.NormalizedKey is not null) return candidate.NormalizedKey == token.NormalizedKey;
        return MatchesValue(candidate.NormalizedKey, token.NormalizedValue);
    }

    private sealed record CandidateTag(Ulid TagId, Ulid CanonicalTagId, string NormalizedKey);

    public static string EndpointName => nameof(ResolveTagLanding);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder) => builder
            .MapGet("tags/resolve", async (
                [AsParameters] ResolveTagLandingQuery query,
                ResolveTagLanding useCase,
                CancellationToken cancellationToken) =>
                (await useCase.HandleAsync(query.Spec, cancellationToken: cancellationToken)).ToOkResult())
            .WithSummary("Resolve Tag Landing Page")
            .WithDescription("Resolves one to three comma-separated tag route tokens to canonical taxonomy families. " +
                "Value-only, synonym, legacy plus-separated, differently ordered, and differently formatted inputs resolve to one canonical specification. " +
                "Returns HTTP 400 for malformed or over-limit input, 404 for unknown tags, and 409 for ambiguous value-only tags.")
            .WithTags(ApiTags.Tags.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(unauthorized: false, forbidden: false)
            .Produces<ResolveTagLandingResponse>();
    }
}
