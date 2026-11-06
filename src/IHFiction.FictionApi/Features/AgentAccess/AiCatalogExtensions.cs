using IHFiction.SharedKernel.AiCatalog;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class AiCatalogExtensions
{
    private const string AiCatalogPath = "/.well-known/ai-catalog.json";
    private const string DidWebPath = "/.well-known/did.json";

    public static IEndpointRouteBuilder MapAiCatalog(this IEndpointRouteBuilder builder)
    {
        builder.MapMethods(AiCatalogPath, [HttpMethods.Get, HttpMethods.Head], () =>
            Results.Json(AiCatalog.CreateDocument(), contentType: "application/json"))
            .ExcludeFromDescription();

        builder.MapMethods(DidWebPath, [HttpMethods.Get, HttpMethods.Head], () =>
            Results.Json(AiCatalog.CreateDidDocument(), contentType: "application/json"))
            .ExcludeFromDescription();

        return builder;
    }
}
