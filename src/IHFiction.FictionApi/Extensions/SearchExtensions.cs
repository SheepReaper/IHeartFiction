using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using IHFiction.FictionApi.Common;
using IHFiction.SharedKernel.Searching;

namespace IHFiction.FictionApi.Extensions;

internal static class SearchExtensions
{
    public static IQueryable<T> SearchContains<T>(
    this IQueryable<T> source,
    string? searchTerm,
    params Expression<Func<T, string?>>[] selectors) => DoSearch(source, searchTerm, selectors, caseInsensitive: false);

    /// <summary>
    /// Applies search filtering to a queryable with multiple field support.
    /// Provides consistent search behavior across different entity types.
    /// </summary>
    public static IQueryable<T> SearchIContains<T>(
        this IQueryable<T> source,
        string? searchTerm,
        params Expression<Func<T, string?>>[] selectors) => DoSearch(source, searchTerm, selectors, caseInsensitive: true);

    public static IQueryable<T> SearchIContains<T>(
        this IQueryable<T> source,
        ISearchSupport request,
        params Expression<Func<T, string?>>[] selectors) => DoSearch(source, request.Search, selectors, caseInsensitive: true);

    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "string.ToUpper is not generic")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "string.ToUpper won't be trimmed")]

    private static IQueryable<T> DoSearch<T>(
        this IQueryable<T> source,
        string? searchTerm,
        Expression<Func<T, string?>>[] selectors,
        bool caseInsensitive)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || selectors is null || selectors.Length == 0)
            return source;

        var sanitizedSearch = InputSanitizationService.SanitizeSearchQuery(searchTerm);
        if (string.IsNullOrEmpty(sanitizedSearch))
            return source;

        // Parameter for the lambda expression
        var parameter = Expression.Parameter(typeof(T), "x");

        // Build the expression: x => x.Property1.Contains(searchTerm) || x.Property2.Contains(searchTerm) || ...
        Expression? combined = null;

        // Pre-normalize the search term if case-insensitive search is requested
        string term = caseInsensitive ? sanitizedSearch.ToUpperInvariant() : sanitizedSearch;

        // MethodInfo for string.Contains
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        foreach (var selector in selectors)
        {
            // Replace the parameter in the selector with the new parameter
            var body = selector.RebindParameter(parameter);

            // Expression to check for null: x.Property != null
            var notNull = Expression.NotEqual(body, Expression.Constant(null, typeof(string)));

            Expression toTest = body;
            if (caseInsensitive)
            {
                // call ToUpperInvariant() on property
                toTest = Expression.Call(body, nameof(string.ToUpper), Type.EmptyTypes);
            }

            var right = Expression.Constant(term, typeof(string));
            var containsCall = Expression.Call(toTest, containsMethod, right);

            // Combine the not-null and contains expressions: x.Property != null && x.Property.Contains(searchTerm)
            var andExpr = Expression.AndAlso(notNull, containsCall);

            combined = combined == null
                ? andExpr
                : Expression.OrElse(combined, andExpr);
        }

        // Final lambda expression: x => (x.Property1 != null && x.Property1.Contains(searchTerm)) || (x.Property2 != null && x.Property2.Contains(searchTerm)) || ...
        var lambda = Expression.Lambda<Func<T, bool>>(combined!, parameter);

        return source.Where(lambda);
    }
}
