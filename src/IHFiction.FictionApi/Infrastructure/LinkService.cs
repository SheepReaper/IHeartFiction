using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Infrastructure;

internal sealed class LinkService(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
{
    public LinkItem Create(
        string endpointName,
        string rel,
        string? method = null,
        IEnumerable<KeyValuePair<string, string?>>? values = null)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor.HttpContext);

        var href = linkGenerator.GetUriByName(
           httpContextAccessor.HttpContext,
           endpointName,
           new RouteValueDictionary(values));

        return new LinkItem(
            href ?? throw new ArgumentException("Link generation failed due to uri generation parameters"),
            rel,
            method ?? HttpMethods.Get);
    }

    public LinkItem Create<TUseCase>(
        string rel,
        string? method = null,
        IEnumerable<KeyValuePair<string, string?>>? values = null) where TUseCase : INameEndpoint<TUseCase> => Create(TUseCase.EndpointName, rel, method, values);
}