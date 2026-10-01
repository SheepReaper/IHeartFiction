using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public sealed class TagService(FictionApiClient client)
{
    public async ValueTask<Result<ResolveTagLandingResponse>> ResolveAsync(
        string specification,
        CancellationToken cancellationToken = default) =>
        await client.ResolveTagLandingAsync(specification, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedPagedCollectionOfListTagsItem>> ListAsync(
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        await client.ListTagsAsync(page, pageSize, null, "value", null, new ListTagsBody(), cancellationToken)
            .HandleApiException();
}
