using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;
using System.Text.Json;

namespace IHFiction.SharedWeb.Services;

public sealed class TagService(FictionApiClient client)
{
    public async ValueTask<Result<ResolveTagLandingResponse>> ResolveAsync(
        string specification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.ResolveTagLandingAsync(specification, cancellationToken);
        }
        catch (ApiException exception)
        {
            var code = exception.StatusCode switch
            {
                400 => "ResolveTagLanding.InvalidSpec",
                404 => "ResolveTagLanding.NotFound",
                409 => "ResolveTagLanding.Conflict",
                _ => "ResolveTagLanding.UpstreamError"
            };
            var description = "Unable to resolve tags. Use one to three comma-separated category:value tags, up to 300 characters.";
            try
            {
                using var document = JsonDocument.Parse(exception.Response);
                if (document.RootElement.TryGetProperty("domainError", out var error)
                    && error.TryGetProperty("description", out var detail))
                    description = detail.GetString() ?? description;
            }
            catch (JsonException)
            {
                // Non-JSON upstream failures retain the safe fallback message.
            }
            return new DomainError(code, description);
        }
    }

    public async ValueTask<Result<LinkedPagedCollectionOfListTagsItem>> ListAsync(
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        await client.ListTagsAsync(page, pageSize, null, "value", null, new ListTagsBody(), cancellationToken)
            .HandleApiException();
}
