using System.ComponentModel.DataAnnotations;

namespace IHFiction.SharedKernel.Validation;

/// <summary>
/// Validates that a string does not contain potentially harmful content such as script tags, 
/// javascript protocols, or event handlers that could be used for XSS attacks.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class NoHarmfulContentAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoHarmfulContentAttribute"/> class.
    /// </summary>
    public NoHarmfulContentAttribute()
    {
        ErrorMessage = "{0} contains potentially harmful content.";
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

        // Check for potentially harmful content (XSS patterns)
        if (ValidationRegexPatterns.HarmfulContent().IsMatch(trimmedValue))
        {
            var memberName = validationContext.MemberName ?? "field";
            return new ValidationResult(
                FormatErrorMessage(memberName),
                validationContext.MemberName != null ? [validationContext.MemberName] : null);
        }

        return ValidationResult.Success;
    }
}
