namespace IHFiction.SharedKernel.Searching;

/// <summary>
/// Interface for endpoints that support search functionality.
/// Provides standardized search parameters with validation.
/// </summary>
public interface ISearchSupport
{
    /// <summary>
    /// Search query string to filter results.
    /// Must be between 2 and 100 characters when provided.
    /// </summary>
    /// <example>fantasy adventure</example>
    string? Search { get; }
}
