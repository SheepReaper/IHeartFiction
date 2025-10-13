using System.Data;
using System.Dynamic;
using System.Linq.Expressions;

using Microsoft.Extensions.DependencyInjection.Extensions;

using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Filtering;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Searching;
using IHFiction.SharedKernel.Sorting;

namespace IHFiction.FictionApi.Extensions;

internal static class PaginationExtensions
{
    public static IServiceCollection AddPagination(this IServiceCollection services, Action<PaginationOptions>? configureOptions = null) =>
        AddPagination(services, PaginationOptions.SectionName, configureOptions);

    public static IServiceCollection AddPagination(this IServiceCollection services, string sectionName, Action<PaginationOptions>? configureOptions = null)
    {
        var builder = services.AddOptions<PaginationOptions>();
        builder.BindConfiguration(sectionName);

        if (configureOptions is not null)
        {
            builder.Configure(configureOptions);
        }

        builder
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IPaginationService, PaginationService>();

        return services;
    }


    /// <summary>
    /// Converts a PagedResult to a different type using a selector function.
    /// </summary>
    public static PagedCollection<TResult> ToPagedResult<TResult>(
        this IQueryable<TResult> itemQuery,
        int totalCount, int page, int pageSize)
    {
        return new PagedCollection<TResult>(
            Data: itemQuery.Skip((page - 1) * pageSize).Take(pageSize),
            TotalCount: totalCount,
            CurrentPage: page,
            PageSize: pageSize);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I really do want to swallow exceptions here")]
    public static Result<LinkedPagedCollection<T>> WithLinks<T>(
        this Result<PagedCollection<T>> sourceResult,
        LinkService linker,
        string endpointName,
        Expression<Func<T, Linked<T>>>? itemMapper = null,
        object? queryParams = null,
        IEnumerable<LinkItem>? extraCollectionLinks = null)
    {
        if (sourceResult.IsFailure)
            return sourceResult.DomainError!;

        try
        {
            var linkValue = WithLinks(
                (ICollectionPaged<T>)sourceResult.Value,
                linker,
                endpointName,
                itemMapper,
                queryParams,
                extraCollectionLinks);

            return linkValue;
        }
        catch (Exception)
        {
            return new DomainError("Internal.LinkGenerationFailed", "Failed to generate links.");
        }
    }

    public static LinkedPagedCollection<T> WithLinks<T>(
        this ICollectionPaged<T> pagedCollection,
        IEnumerable<LinkItem> collectionLinks,
        Expression<Func<T, Linked<T>>>? itemMapper = null) where T : ILinks
    {
        itemMapper ??= e => new(e, Enumerable.Empty<LinkItem>());
        return new(
            pagedCollection.Data.Select(itemMapper),
            pagedCollection.TotalCount,
            pagedCollection.CurrentPage,
            pagedCollection.PageSize,
            collectionLinks);
    }

    public static LinkedPagedCollection<T> WithLinks<TUseCase, T>(
        this ICollectionPaged<T> pagedCollection,
        LinkService linker,
        Expression<Func<T, Linked<T>>>? itemMapper = null,
        object? queryParams = null,
        IEnumerable<LinkItem>? extraCollectionLinks = null) where TUseCase : INameEndpoint<TUseCase> =>
            WithLinks(pagedCollection, linker, TUseCase.EndpointName, itemMapper, queryParams, extraCollectionLinks);

    // public static Linked<T> WithLinks<T>(
    //     this T sourceItem,
    //     IEnumerable<LinkItem> links
    // ) => new(sourceItem, links);

    public static Result<Linked<T>> WithLinks<T>(
        this IDomainResult<T> sourceResult,
        IEnumerable<LinkItem>? links = null
    )
    {
        return sourceResult.IsFailure
            ? sourceResult.DomainError! 
            : new Linked<T>(sourceResult.Value, links ?? []);
    }

    public static LinkedPagedCollection<T> WithLinks<T>(
        this ICollectionPaged<T> pagedCollection,
        LinkService linker,
        string endpointName,
        Expression<Func<T, Linked<T>>>? itemMapper = null,
        object? queryParams = null,
        IEnumerable<LinkItem>? extraCollectionLinks = null)
    {
        itemMapper ??= e => new(e, Enumerable.Empty<LinkItem>());

        ExpandoObject valuesObj = new();
        IDictionary<string, object?> values = valuesObj;

        values.Add(nameof(IPaginationSupport.Page), pagedCollection.CurrentPage);
        values.Add(nameof(IPaginationSupport.PageSize), pagedCollection.PageSize);

        if (queryParams is IFilterSupport f) values.Add(nameof(IFilterSupport.Filter), f.Filter);
        if (queryParams is ISearchSupport se) values.Add(nameof(ISearchSupport.Search), se.Search);
        if (queryParams is ISortingSupport so) values.Add(nameof(ISortingSupport.Sort), so.Sort);

        List<LinkItem> links = [linker.Create(endpointName, "self", values: valuesObj.Clone())];

        if (pagedCollection.PageSize * pagedCollection.CurrentPage < pagedCollection.TotalCount)
        {
            values[nameof(IPaginationSupport.Page)] = pagedCollection.CurrentPage + 1;

            links.Add(linker.Create(endpointName, "next-page", values: valuesObj.Clone()));
        }

        if (pagedCollection.CurrentPage > 1)
        {
            values[nameof(IPaginationSupport.Page)] = pagedCollection.CurrentPage - 1;

            links.Add(linker.Create(endpointName, "previous-page", values: values));
        }

        if (extraCollectionLinks is not null)
            links.AddRange(extraCollectionLinks);

        return new(
            pagedCollection.Data.Select(itemMapper),
            pagedCollection.TotalCount,
            pagedCollection.CurrentPage,
            pagedCollection.PageSize,
            links);
    }

    public static ExpandoObject Clone(this ExpandoObject source)
    {
        ExpandoObject target = new();
        IDictionary<string, object?> original = source;
        IDictionary<string, object?> clone = target;

        foreach (var kvp in original)
            clone.Add(kvp);

        return target;
    }
}
