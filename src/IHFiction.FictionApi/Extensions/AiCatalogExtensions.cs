using IHFiction.SharedKernel.AiCatalog;

namespace IHFiction.FictionApi.Extensions;

internal static class AiCatalogExtensions
{
    private const string AiCatalogPath = "/.well-known/ai-catalog.json";

    public static IEndpointRouteBuilder MapAiCatalog(this IEndpointRouteBuilder builder)
    {
        builder.MapMethods(AiCatalogPath, [HttpMethods.Get, HttpMethods.Head], () =>
            Results.Json(AiCatalog.CreateDocument(), contentType: "application/json"))
            .ExcludeFromDescription();

        return builder;
    }
}
