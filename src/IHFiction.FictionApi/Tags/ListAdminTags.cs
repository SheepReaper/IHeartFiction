using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.FictionApi.Tags;

internal sealed class ListAdminTags(
    FictionDbContext context,
    IPaginationService paginator) : IUseCase, INameEndpoint<ListAdminTags>
{
    internal sealed record ListAdminTagsQuery(
        [property: Range(1, int.MaxValue)]
        int Page = 1,

        [property: Range(1, 200)]
        int PageSize = 50,

        [property: StringLength(100)]
        string Search = "",

        [property: StringLength(50)]
        [property: ShapesType<AdminTagItem>]
        string Fields = "") : IPaginationSupport, IDataShapingSupport;

    internal sealed record AdminTagItem(
        Ulid TagId,
        string Category,
        string? Subcategory,
        string Value,
        string DisplayFormat,
        IReadOnlyCollection<string> Synonyms,
        int PublishedWorkCount);

    public async Task<Result<PagedCollection<AdminTagItem>>> HandleAsync(
        ListAdminTagsQuery query,
        CancellationToken cancellationToken = default)
    {
        var search = query.Search.Trim();

        var tags = context.Tags
            .AsNoTracking()
            .OfType<CanonicalTag>()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            tags = tags.Where(tag =>
                EF.Functions.ILike(tag.Category, $"%{search}%") ||
                (tag.Subcategory != null && EF.Functions.ILike(tag.Subcategory, $"%{search}%")) ||
                EF.Functions.ILike(tag.Value, $"%{search}%") ||
                tag.Synonyms.Any(synonym => EF.Functions.ILike(synonym.Value, $"%{search}%")));
        }

        var projection = tags
            .OrderBy(tag => tag.Category)
            .ThenBy(tag => tag.Subcategory)
            .ThenBy(tag => tag.Value)
            .Select(tag => new AdminTagItem(
                tag.Id,
                tag.Category,
                tag.Subcategory,
                tag.Value,
                tag.Subcategory == null
                    ? $"{tag.Category}:{tag.Value}"
                    : $"{tag.Category}:{tag.Subcategory}:{tag.Value}",
                tag.Synonyms
                    .Select(synonym => synonym.Subcategory == null
                        ? $"{synonym.Category}:{synonym.Value}"
                        : $"{synonym.Category}:{synonym.Subcategory}:{synonym.Value}")
                    .ToArray(),
                tag.Works.Count(work => work.PublishedAt != null)))
            .AsQueryable();

        return await paginator.ExecutePagedQueryAsync(projection, query, cancellationToken);
    }

    public static string EndpointName => nameof(ListAdminTags);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("admin/tags", async (
                [AsParameters] ListAdminTagsQuery query,
                ListAdminTags useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = (await useCase.HandleAsync(query, cancellationToken))
                    .WithLinks(linker, Name, tag => new(tag, new List<LinkItem>()), query);

                return result.ToOkResult(query);
            })
            .WithSummary("List Tags for Administration")
            .WithDescription("Lists canonical tags, their synonyms, and published usage counts for administrators.")
            .WithTags(ApiTags.Tags.Administration)
            .RequireAuthorization("admin")
            .WithStandardResponses(notFound: false)
            .Produces<LinkedPagedCollection<AdminTagItem>>();
        }
    }
}