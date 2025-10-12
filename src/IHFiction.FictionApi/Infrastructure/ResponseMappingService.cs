using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Infrastructure;

/// <summary>
/// Centralized service for mapping domain results to HTTP responses.
/// Eliminates duplicate error mapping patterns and provides consistent HTTP status codes.
/// </summary>
internal static class ResponseMappingService
{
    /// <summary>
    /// Maps a domain result to an appropriate HTTP result with standardized error handling.
    /// Centralizes the error mapping pattern used across multiple endpoints.
    /// </summary>
    public static IResult MapToHttpResult<T>(Result<T> result, Func<T, IResult>? successMapper = null)
    {
        return result.IsSuccess ? successMapper?.Invoke(result.Value) ?? Results.Ok(result.Value) : MapErrorToHttpResult(result.DomainError);
    }

    /// <summary>
    /// Maps a simple success result (no return value) to HTTP result.
    /// Used for operations that don't return data (like delete operations).
    /// </summary>
    public static IResult MapToHttpResult(Result result)
    {
        return result.IsSuccess 
            ? Results.Ok() 
            : MapErrorToHttpResult(result.DomainError);
    }

    /// <summary>
    /// Maps a domain result to a Created HTTP result with location header.
    /// Standardizes the creation response pattern used in POST endpoints.
    /// </summary>
    public static IResult CreatedResult<T>(Result<T> result, Func<T, string> locationFactory)
        where T : class
    {
        return MapToHttpResult(result, value => Results.Created(new Uri(locationFactory(value), UriKind.Relative), value));
    }

    /// <summary>
    /// Maps a domain result to a Created HTTP result with route-based location.
    /// Standardizes the creation response pattern for endpoints with route names.
    /// </summary>
    public static IResult CreatedAtRouteResult<T>(
        Result<T> result, 
        string routeName, 
        Func<T, object> routeValuesFactory)
        where T : class
    {
        return MapToHttpResult(result, value => 
            Results.CreatedAtRoute(routeName, routeValuesFactory(value), value));
    }

    /// <summary>
    /// Maps a domain error to an appropriate HTTP result.
    /// Provides consistent error response format across all endpoints.
    /// </summary>
    private static IResult MapErrorToHttpResult(DomainError error)
    {
        return error.Code switch
        {
            // Authentication and Authorization Errors
            "Author.NotRegistered" => Results.Forbid(),

            "Author.NotFound" => Results.NotFound(new {
                message = error.Description,
                code = error.Code
            }),
            
            "Auth.InsufficientPermissions" => Results.Forbid(),
            
            "ClaimsPrincipal.MissingClaim" or "ClaimsPrincipal.UnparsableId" => Results.Forbid(),
            
            // Authorization-specific errors with more context
            var code when code.EndsWith("NotAuthorized", StringComparison.Ordinal) => Results.Forbid(),
            
            // Resource Not Found Errors
            "Story.NotFound" => Results.NotFound(new {
                message = "Story not found.",
                code = error.Code
            }),
            
            "Chapter.NotFound" => Results.NotFound(new {
                message = "Chapter not found.",
                code = error.Code
            }),
            
            "General.NotFound" => Results.NotFound(new {
                message = error.Description,
                code = error.Code
            }),
            
            // Conflict Errors
            var code when code.EndsWith(".TitleExists", StringComparison.Ordinal) => Results.Conflict(new {
                message = error.Description,
                code = error.Code
            }),
            
            "Database.ConcurrencyConflict" => Results.InternalServerError(new {
                message = error.Description,
                code = error.Code
            }),
            
            var code when code.EndsWith(".AlreadyDeleted", StringComparison.Ordinal) => Results.Conflict(new {
                message = error.Description,
                code = error.Code
            }),

            // Validation Errors (400 Bad Request)
            var code when code.StartsWith("Validation.", StringComparison.Ordinal) => Results.BadRequest(new {
                message = error.Description,
                code = error.Code
            }),

            var code when code.Contains(".Invalid", StringComparison.Ordinal) => Results.BadRequest(new {
                message = error.Description,
                code = error.Code
            }),
            
            // Database and Infrastructure Errors (500 Internal Server Error)
            "Database.SaveFailed" or "Database.ConnectionFailed" => Results.Problem(
                statusCode: 500,
                title: "Internal Server Error",
                detail: error.Description,
                type: error.Code),
            
            // Content-related errors
            "Content.NotFound" => Results.NotFound(new {
                message = "Content not found.",
                code = error.Code
            }),
            
            "Content.TooLarge" => Results.BadRequest(new {
                message = error.Description,
                code = error.Code
            }),
            
            // Default fallback - use the extension method for unhandled cases
            _ => error.ToProblemDetailsResult()
        };
    }

    /// <summary>
    /// Creates a standardized validation error response.
    /// Used when request validation fails before reaching the use case.
    /// </summary>
    public static IResult ValidationErrorResult(IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> errors)
    {
        return errors.ValidationProblem();
    }

    /// <summary>
    /// Creates a standardized validation error response from error messages.
    /// Used when custom validation fails.
    /// </summary>
    public static IResult ValidationErrorResult(IEnumerable<string> errorMessages)
    {
        var validationErrors = errorMessages.Select(msg => 
            new System.ComponentModel.DataAnnotations.ValidationResult(msg));
        return ValidationErrorResult(validationErrors);
    }

    /// <summary>
    /// Maps a simple success result to NoContent (204) response.
    /// Used for update operations that don't return updated data.
    /// </summary>
    public static IResult NoContentResult(Result result)
    {
        return result.IsSuccess 
            ? Results.NoContent() 
            : MapErrorToHttpResult(result.DomainError);
    }

    /// <summary>
    /// Creates a custom success response with specific status code.
    /// Used when you need a non-standard success response.
    /// </summary>
    public static IResult CustomSuccessResult<T>(
        Result<T> result, 
        int statusCode, 
        Func<T, object>? responseMapper = null)
    {
        if (result.IsSuccess)
        {
            var responseData = responseMapper?.Invoke(result.Value) ?? result.Value;
            return Results.Json(responseData, statusCode: statusCode);
        }

        return MapErrorToHttpResult(result.DomainError);
    }
}
