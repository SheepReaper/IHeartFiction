namespace IHFiction.FictionApi.Extensions;

/// <summary>
/// Extension methods for enhancing endpoint documentation with comprehensive OpenAPI metadata.
/// </summary>
internal static class EndpointDocumentationExtensions
{
    internal class StandardResponseOptions
    {
        public bool Conflict { get; set; } = true;
        public bool Forbidden { get; set; } = true;
        public bool NotFound { get; set; } = true;
        public bool Unauthorized { get; set; } = true;
        public bool Validation { get; set; } = true;
        public IDictionary<string, (Type, int?)> ContentTypeResponseMap { get; set; } = new Dictionary<string, (Type, int?)>();

        public static StandardResponseOptions Default => new();

        public StandardResponseOptions WithProduces<T>(string contentType, int statusCode)
        {
            ContentTypeResponseMap[contentType] = (typeof(T), statusCode);
            return this;
        }
    }

    /// <summary>
    /// Adds standard error response documentation for common HTTP status codes.
    /// </summary>
    /// <param name="builder">The route handler builder</param>
    /// <param name="options">Standard response options</param>
    /// <returns>The route handler builder for method chaining</returns>
    public static RouteHandlerBuilder WithStandardResponses(
        this RouteHandlerBuilder builder,
        StandardResponseOptions options)
    {
        if (options.Validation)
        {
            builder = builder.ProducesValidationProblem();
        }

        if (options.Unauthorized)
        {
            builder = builder.ProducesProblem(StatusCodes.Status401Unauthorized);
        }

        if (options.Forbidden)
        {
            builder = builder.ProducesProblem(StatusCodes.Status403Forbidden);
        }

        if (options.NotFound)
        {
            builder = builder.ProducesProblem(StatusCodes.Status404NotFound);
        }

        if (options.Conflict)
        {
            builder = builder.ProducesProblem(StatusCodes.Status409Conflict);
        }

        foreach (var (contentType, (responseType, statusCode)) in options.ContentTypeResponseMap)
        {
            builder = builder.Produces(statusCode ?? StatusCodes.Status200OK, responseType, contentType);
        }

        return builder;
    }

    public static RouteHandlerBuilder WithStandardResponses(
        this RouteHandlerBuilder builder,
        bool conflict = true,
        bool forbidden = true,
        bool notFound = true,
        bool unauthorized = true,
        bool validation = true) => WithStandardResponses(builder,
            new()
            {
                Conflict = conflict,
                Forbidden = forbidden,
                NotFound = notFound,
                Unauthorized = unauthorized,
                Validation = validation
            });
}
