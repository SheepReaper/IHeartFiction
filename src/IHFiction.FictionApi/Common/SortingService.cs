using Microsoft.EntityFrameworkCore;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Centralized service for handling sorting logic across list endpoints.
/// Provides consistent sorting behavior and validation for query operations.
/// </summary>
internal static class SortingService
{
    /// <summary>
    /// Validates sort parameters against allowed fields and orders.
    /// Provides consistent sort validation across all endpoints.
    /// </summary>
    /// <param name="sortBy">The field to sort by</param>
    /// <param name="sortOrder">The sort direction (asc/desc)</param>
    /// <param name="allowedSortFields">Optional array of allowed sort fields for validation</param>
    /// <returns>Collection of validation error messages</returns>
    public static IEnumerable<string> ValidateSortParameters(
        string sortBy,
        string sortOrder,
        string[]? allowedSortFields = null)
    {
        var errors = new List<string>();

        // Validate sort order
        if (sortOrder != "asc" && sortOrder != "desc")
            errors.Add("Sort order must be either 'asc' or 'desc'.");

        // Validate sort field if allowed fields are specified
        if (allowedSortFields != null && !allowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Sort field '{sortBy}' is not allowed. Allowed fields: {string.Join(", ", allowedSortFields)}.");

        return errors;
    }

    /// <summary>
    /// Normalizes sort parameters with appropriate defaults and sanitization.
    /// Ensures consistent sort parameter handling across endpoints.
    /// </summary>
    /// <param name="sortBy">The field to sort by</param>
    /// <param name="sortOrder">The sort direction</param>
    /// <param name="defaultSortBy">Default sort field if none provided</param>
    /// <param name="defaultSortOrder">Default sort order if none provided</param>
    /// <param name="allowedSortFields">Optional array of allowed sort fields</param>
    /// <returns>Normalized sort parameters</returns>
    public static (string sortBy, string sortOrder) NormalizeSortParameters(
        string? sortBy,
        string? sortOrder,
        string defaultSortBy = "updated",
        string defaultSortOrder = "desc",
        string[]? allowedSortFields = null)
    {
        // Use InputSanitizationService for sanitization
        var (cleanSortBy, cleanSortOrder) = InputSanitizationService.SanitizeSortParameters(
            sortBy, sortOrder, defaultSortBy, defaultSortOrder, allowedSortFields);

        return (cleanSortBy, cleanSortOrder);
    }

    /// <summary>
    /// Creates a sort request with validation.
    /// Combines normalization and validation in a single operation.
    /// </summary>
    /// <param name="sortBy">The field to sort by</param>
    /// <param name="sortOrder">The sort direction</param>
    /// <param name="defaultSortBy">Default sort field if none provided</param>
    /// <param name="defaultSortOrder">Default sort order if none provided</param>
    /// <param name="allowedSortFields">Optional array of allowed sort fields</param>
    /// <returns>Tuple containing normalized sort parameters and any validation errors</returns>
    public static ((string sortBy, string sortOrder) sortParams, IEnumerable<string> errors) CreateSortRequestWithValidation(
        string? sortBy,
        string? sortOrder,
        string defaultSortBy = "updated",
        string defaultSortOrder = "desc",
        string[]? allowedSortFields = null)
    {
        var normalizedParams = NormalizeSortParameters(sortBy, sortOrder, defaultSortBy, defaultSortOrder, allowedSortFields);
        var errors = ValidateSortParameters(normalizedParams.sortBy, normalizedParams.sortOrder, allowedSortFields);
        
        return (normalizedParams, errors);
    }
}
