using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace IHFiction.SharedKernel.Sorting;

public static class Extensions
{
    public static void EnsureValid(this SortMapping[] mappings, ISortingSupport request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureValid(mappings, request.Sort);
    }

    public static void EnsureValid(this SortMapping[] mappings, string? sortParam = null)
    {
        if (!TryValidate(mappings, out var errors, sortParam))
        {
            throw new ValidationException(errors, null, null);
        }
    }
    public static bool Validate(this SortMapping[] mappings, ISortingSupport request) =>
        // ArgumentNullException.ThrowIfNull(request);

        TryValidate(mappings, out _, request);

    public static bool Validate(this SortMapping[] mappings, string? sortParam = null) => TryValidate(mappings, out _, sortParam);

    public static bool TryValidate(this SortMapping[] mappings, [NotNullWhen(false)] out ValidationResult? errors, ISortingSupport request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryValidate(mappings, out errors, request.Sort);
    }

    public static bool TryValidate(this SortMapping[] mappings, [NotNullWhen(false)] out ValidationResult? errors, string? sortParam = null)
    {
        errors = null;

        if (string.IsNullOrWhiteSpace(sortParam))
        {
            return true;
        }

        // string[] sortFields = [.. sortParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => p.Split(' ')[0])];

        var sortTerms = sortParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        if (sortTerms.Length == 0)
        {
            errors = new ValidationResult("Sort must include at least one field.");
            return false;
        }

        string[] invalidFields = [.. sortTerms
            .Where(parts => parts.Length == 0 || !mappings.Any(mapping =>
                mapping.SortField.Equals(parts[0], StringComparison.OrdinalIgnoreCase)))
            .Select(parts => parts.Length == 0 ? string.Empty : parts[0])];

        if (invalidFields.Length > 0)
        {
            errors = new ValidationResult("Sort field is invalid.", invalidFields);
            return false;
        }

        if (sortTerms.Any(parts => parts.Length > 2 ||
            (parts.Length == 2 &&
             !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase) &&
             !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))))
        {
            errors = new ValidationResult("Sort direction must be 'asc' or 'desc'.");
            return false;
        }

        return true;
    }
}
