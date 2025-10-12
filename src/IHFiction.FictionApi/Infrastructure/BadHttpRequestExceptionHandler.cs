using Microsoft.AspNetCore.Diagnostics;

using IHFiction.FictionApi.Extensions;

namespace IHFiction.FictionApi.Infrastructure;

internal sealed class BadHttpRequestExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<BadHttpRequestExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        const int statusCode = StatusCodes.Status400BadRequest;

        if (exception is not BadHttpRequestException badHttpRequestException) return false;

        logger.ValidationError(exception);

        httpContext.Response.StatusCode = statusCode;

        ProblemDetailsContext context = new()
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new()
            {
                Title = "Bad Request",
                Detail = badHttpRequestException.Message,
                Status = statusCode,
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1"
            }
        };

        return await problemDetailsService.TryWriteAsync(context);
    }
}
