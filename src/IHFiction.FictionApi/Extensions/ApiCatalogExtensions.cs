using System.Net.Mime;

using Microsoft.Extensions.Options;

using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.ApiCatalog;

namespace IHFiction.FictionApi.Extensions;

internal static class ApiCatalogExtensions
{
    private const string LinksetMediaTypeName = "application/linkset+json";
    private const string OpenApiMediaTypeName = "application/vnd/oai/openapi+json";
    private const string DefaultPattern = "/.well-known/api-catalog";
    private const string LinksetContentType = $"{LinksetMediaTypeName}; profile=\"https://www.rfc-editor.org/info/rfc9727\"";

    public static RouteHandlerBuilder MapApiCatalog(this IEndpointRouteBuilder builder, string pattern = DefaultPattern)
    {
        builder
            .MapMethods(pattern, [HttpMethods.Head], (HttpContext context, IOptions<BaseUrlOptions> options) =>
            {
                var catalog = new Uri(options.Value.BaseUrl!, pattern);

                context.Response.Headers.Link = $"<{catalog}>; rel=\"api-catalog\"; type=\"{LinksetMediaTypeName}\"";

                return Results.Ok();
            })
            .ExcludeFromDescription();

        return builder
            .MapGet(pattern, (IOptions<BaseUrlOptions> options) =>
            {
                var origin = options.Value.BaseUrl!.ToString().TrimEnd('/');

                LinksetDocument catalog = new([
                    new(
                        Anchor: $"{origin}/",
                        ServiceDescription: [
                            new(
                                $"{origin}/openapi/v1.json",
                                OpenApiMediaTypeName
                            )
                        ],
                        ServiceDocumentation: [
                            new(
                                $"{origin}/scalar/v1",
                                MediaTypeNames.Text.Html
                            )
                        ]
                    )
                ]);

                return Results.Json(catalog, contentType: LinksetContentType);
            })
            .ExcludeFromDescription();
    }
}
