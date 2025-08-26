using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Diagnostics;

using IHFiction.FictionApi.Extensions;

namespace IHFiction.FictionApi.Common;
internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        const int statusCode = StatusCodes.Status400BadRequest;

        if (exception is not ValidationException validationException) return false;

        logger.ValidationError(exception);

        httpContext.Response.StatusCode = statusCode;

        ProblemDetailsContext context = new()
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new()
            {
                Title = "Validation Erorr",
                Detail = "One or more validation errors occurred.",
                Status = statusCode,
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1"
            }
        };

        // get the errors, grouped by property name, as a dictionary keyed by the property name and the value is a list of error messages
        // Do not use FluentValidation ValidationException here, as it is not a ValidationException from System.ComponentModel.DataAnnotations
        var errors = validationException.ValidationResult.MemberNames.Select(member => (member, validationException.ValidationResult.ErrorMessage ?? string.Empty))
            .GroupBy(pair => pair.member, pair => pair.Item2)
            .ToDictionary(group => group.Key, group => group.ToArray());

        context.ProblemDetails.Extensions.Add("errors", errors);

        return await problemDetailsService.TryWriteAsync(context);
    }
}
