using System.ComponentModel.DataAnnotations;

namespace IHFiction.FictionApi.Extensions;


internal static class ValidationExtensions
{
    public static void EnsureValid<T>(this T obj) where T : new()
    {
        obj ??= new T();

        Validator.ValidateObject(obj, new ValidationContext(obj), true);
    }

    public static bool IsValid<T>(this T? obj, out HashSet<ValidationResult> results)
    {
        results = [];

        if (obj is null)
        {
            if (typeof(T).IsClass || typeof(T).IsValueType)
            {
                obj = Activator.CreateInstance<T>();
            }
            else
            {
                return false;
            }
        }

        return obj is not null && Validator.TryValidateObject(obj, new ValidationContext(obj), results, true);
    }

    public static IResult ValidationProblem(this IEnumerable<ValidationResult> errors) => ValidationProblem(errors.ToDictionary());

    public static IResult ValidationProblem(this IEnumerable<KeyValuePair<string, string[]>> errors) => Results.ValidationProblem(
        errors: errors,
        type: "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1");
}
