namespace IHFiction.SharedKernel.Sorting;

/// <summary>
/// Interface for endpoints that support sorting functionality.
/// Provides standardized sorting parameters.
/// </summary>
public interface ISortingSupport
{
    /// <summary>
    /// Field to sort the results by.
    /// The allowed values depend on the specific endpoint implementation.
    /// Defaults to implementation-specific value if not specified.
    /// </summary>
    /// <example>name</example>
    string Sort { get; }
}
