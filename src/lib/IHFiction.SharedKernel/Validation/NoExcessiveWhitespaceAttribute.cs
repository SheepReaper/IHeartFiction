using System.ComponentModel.DataAnnotations;

namespace IHFiction.SharedKernel.Validation;

/// <summary>
/// Validates that a string does not contain excessive consecutive whitespace characters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class NoExcessiveWhitespaceAttribute : ValidationAttribute
{
    /// <summary>
    /// The maximum number of consecutive whitespace characters allowed.
    /// </summary>
    public int MaxConsecutiveWhitespace { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoExcessiveWhitespaceAttribute"/> class.
    /// </summary>
    /// <param name="maxConsecutiveWhitespace">The maximum number of consecutive whitespace characters allowed. Default is 2.</param>
    public NoExcessiveWhitespaceAttribute(int maxConsecutiveWhitespace = 2)
    {
        MaxConsecutiveWhitespace = maxConsecutiveWhitespace;
        ErrorMessage = $"{{0}} contains excessive whitespace. Please format your {{0}} properly.";
    }

    /// <summary>
    /// Validates the specified value with respect to the current validation attribute.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The context information about the validation operation.</param>
    /// <returns>An instance of the <see cref="ValidationResult"/> class.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is null or string { Length: 0 })
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("Value must be a string.");
        }

        var trimmedValue = stringValue.Trim();
        if (string.IsNullOrEmpty(trimmedValue))
        {
            return ValidationResult.Success;
        }

        // Check for excessive consecutive whitespace based on the threshold
        var isExcessive = MaxConsecutiveWhitespace switch
        {
            >= 5 => ValidationRegexPatterns.ExcessiveWhitespace5Plus().IsMatch(trimmedValue),
            >= 3 => ValidationRegexPatterns.ExcessiveWhitespace3Plus().IsMatch(trimmedValue),
            _ => ValidationRegexPatterns.ConsecutiveWhitespace().IsMatch(trimmedValue) &&
                 trimmedValue.Contains(new string(' ', MaxConsecutiveWhitespace + 1), StringComparison.Ordinal)
        };

        if (isExcessive)
        {
            var memberName = validationContext.MemberName ?? "field";
            return new ValidationResult(
                FormatErrorMessage(memberName),
                validationContext.MemberName != null ? [validationContext.MemberName] : null);
        }

        return ValidationResult.Success;
    }
}
