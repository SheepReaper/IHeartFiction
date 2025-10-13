using Microsoft.AspNetCore.Http.HttpResults;

using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Extensions;

internal static class ErrorExtensions
{
    public static IResult ToProblemDetailsResult(this DomainError error)
    {
        var extensions = new Dictionary<string, object?>()
            {
                {"domainError", new{error.Code, error.Description}}
            };

        return Results.Problem(
            statusCode: GetStatusCode(error.Code),
            // detail: error.Description,
            extensions: extensions);
    }

    public static ProblemHttpResult ToProblemDetailsTypedResult(this DomainError error)
    {
        var extensions = new Dictionary<string, object?>()
            {
                {"domainError", new{error.Code, error.Description}}
            };

        return TypedResults.Problem(statusCode: GetStatusCode(error.Code),
            // detail: error.Description,
            extensions: extensions);
    }

    private static int GetStatusCode(string code) => code.Split('.').LastOrDefault()?.ToUpperInvariant() switch
    {
        // Limit to 400, 401, 403, 404, 405, 406, 408,
        // 409, 412, 415, 422, 426, 500, 502, 503, 504
        // As those are the ones configured in problem details defaults
        "UNAUTHENTICATED" => StatusCodes.Status401Unauthorized,
        "UNAUTHORIZED" => StatusCodes.Status403Forbidden,
        "NOTAUTHORIZED" => StatusCodes.Status403Forbidden,
        "NOTREGISTERED" => StatusCodes.Status403Forbidden,
        "INVALIDCLAIMS" => StatusCodes.Status403Forbidden,
        "INSUFFICIENTPERMISSIONS" => StatusCodes.Status403Forbidden,
        "NOTFOUND" => StatusCodes.Status404NotFound,
        "ALREADYDELETED" => StatusCodes.Status404NotFound,
        "CONFLICT" => StatusCodes.Status409Conflict,
        "EXISTS" => StatusCodes.Status409Conflict,
        "CONCURRENCYCONFLICT" => StatusCodes.Status500InternalServerError,
        "SAVEFAILED" => StatusCodes.Status500InternalServerError,
        "CONNECTIONFAILED" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError
    };
}
