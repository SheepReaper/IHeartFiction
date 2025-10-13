using System.ComponentModel.DataAnnotations;

namespace IHFiction.FictionApi.Extensions;


internal static class ValidationExtensions
{
    public static IResult ValidationProblem(this IEnumerable<ValidationResult> errors) => ValidationProblem(errors.ToDictionary());

    public static IResult ValidationProblem(this IEnumerable<KeyValuePair<string, string[]>> errors) => Results.ValidationProblem(
        errors: errors,
        type: "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1");
}
