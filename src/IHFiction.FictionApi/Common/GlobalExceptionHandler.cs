using Microsoft.AspNetCore.Diagnostics;

namespace IHFiction.FictionApi.Common;

internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        return problemDetailsService.TryWriteAsync(new()
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new()
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "An error occurred while processing your request.",
                Detail = "An unexpected error occurred while processing your request."
            }
        });
    }
}
