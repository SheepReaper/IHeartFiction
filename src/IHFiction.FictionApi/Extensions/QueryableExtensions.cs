using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

using IHFiction.SharedKernel.Sorting;

namespace IHFiction.FictionApi.Extensions;

internal static class QueryableExtensions
{
    public static Expression RebindParameter<T>(this Expression<Func<T, string?>> expression, ParameterExpression newParameter)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var visitor = new ParameterRebinder(expression.Parameters[0], newParameter);

        return visitor.Visit(expression.Body);
    }

    private sealed class ParameterRebinder(ParameterExpression oldParameter, ParameterExpression newParameter) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == oldParameter ? newParameter : base.VisitParameter(node);
        }
    }

    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        ISortingSupport sort,
        SortMapping[] mappings,
        string defaultOrderBy = "Id")
    {
        ArgumentNullException.ThrowIfNull(sort);
        
        return ApplySort(query, sort.Sort, mappings, defaultOrderBy);
    }

    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sort,
        SortMapping[] mappings,
        string defaultOrderBy = "Id")
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return query.OrderBy(defaultOrderBy);
        }

        string[] sortFields = [.. sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        List<string> orderByParts = [];

        foreach (var field in sortFields)
        {
            var (sortField, isDescending) = ParseSortField(field);

            var mapping = mappings.First(m =>
                m.SortField.Equals(sortField, StringComparison.OrdinalIgnoreCase));

            string direction = (isDescending, mapping.Reverse) switch
            {
                (false, false) => "ASC",
                (false, true) => "DESC",
                (true, false) => "DESC",
                (true, true) => "ASC"
            };

            orderByParts.Add($"{mapping.PropertyName} {direction}");
        }

        string orderBy = string.Join(',', orderByParts);

        return query.OrderBy(orderBy);
    }

    private static (string SortField, bool IsDescending) ParseSortField(string field)
    {
        string[] parts = field.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string sortField = parts[0];
        bool isDescending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortField, isDescending);
    }
}