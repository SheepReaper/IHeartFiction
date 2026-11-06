using System.ComponentModel.DataAnnotations;

namespace IHFiction.SharedKernel.Sorting;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public abstract class SortValidationAttribute(Type type) : ValidationAttribute
{
    public Type Type => type;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not string sortParam)
        {
            return new ValidationResult("Sort must be a string or null.");
        }

        var mappings = SortMappingRegistry.GetMappings(type);
        return mappings.TryValidate(out var errors, sortParam)
            ? ValidationResult.Success
            : errors;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SortsTypeAttribute(Type type) : SortValidationAttribute(type);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SortsTypeAttribute<T>() : SortValidationAttribute(typeof(T));
