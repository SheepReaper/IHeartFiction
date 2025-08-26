using System.ComponentModel.DataAnnotations;

namespace IHFiction.SharedKernel.DataShaping;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public abstract class ResponseShapingAttribute(Type type) : ValidationAttribute
{
    public Type Type => type;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        return DataShapingService.TryValidate(type, value, out var errors)
            ? ValidationResult.Success
            : errors;
    }
}
