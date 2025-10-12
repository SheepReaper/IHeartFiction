using System.ComponentModel.DataAnnotations;

namespace IHFiction.FictionApi.Infrastructure;

/// <summary>
/// Configuration options for pagination behavior across the application.
/// Provides centralized configuration for pagination defaults and limits.
/// </summary>
internal sealed class PaginationOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Pagination";

    /// <summary>
    /// Default page number when none is specified.
    /// Must be greater than 0.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = nameof(DefaultPage) + " must be greater than 0.")]
    public int DefaultPage { get; set; } = 1;

    /// <summary>
    /// Default number of items per page when none is specified.
    /// Must be greater than 0 and less than or equal to MaxPageSize.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = nameof(DefaultPageSize) + " must be greater than 0.")]
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Maximum allowed page size to prevent performance issues.
    /// Must be greater than 0.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = nameof(MaxPageSize) +" must be greater than 0.")]
    public int MaxPageSize { get; set; } = 100;
}
