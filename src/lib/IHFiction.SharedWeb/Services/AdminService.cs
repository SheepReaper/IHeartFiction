using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class AdminService(FictionApiClient client)
{
    public async ValueTask<Result<LinkedPagedCollectionOfAdminTagItem>> ListAdminTagsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => await client.ListAdminTagsAsync(page, pageSize, search, null, cancellationToken).HandleApiException();

    public async ValueTask<Result<RenameCanonicalTagResponse>> RenameCanonicalTagAsync(
        Ulid tagId,
        string category,
        string? subcategory,
        string value,
        string reason,
        CancellationToken cancellationToken = default)
        => await client.RenameCanonicalTagAsync(
            tagId,
            new RenameCanonicalTagBody
            {
                Category = category,
                Subcategory = subcategory,
                Value = value,
                Reason = reason
            },
            cancellationToken).HandleApiException();

    public async ValueTask<Result<MergeCanonicalTagsResponse>> MergeCanonicalTagsAsync(
        Ulid canonicalTagId,
        IReadOnlyCollection<Ulid> sourceTagIds,
        string reason,
        CancellationToken cancellationToken = default)
        => await client.MergeCanonicalTagsAsync(
            new MergeCanonicalTagsBody
            {
                CanonicalTagId = canonicalTagId,
                SourceTagIds = sourceTagIds.Select(id => id.ToString()).ToArray(),
                Reason = reason
            },
            cancellationToken).HandleApiException();
}
