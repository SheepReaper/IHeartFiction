using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Features.Discovery;

public class StoryDiscoveryService(FictionApiClient client)
{
    public async ValueTask<Result<LinkedPagedCollectionOfListPublishedStoryChaptersItem>> ListStoryChaptersAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default) => storyId == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.ListPublishedStoryChaptersAsync(storyId, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedPagedCollectionOfListPublishedStoryChaptersItem>> ListStoryChaptersAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default)
        => !Ulid.TryParse(storyId, out var ulid)
            ? DomainError.InvalidUlid
            : await ListStoryChaptersAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfGetPublishedStoryResponse>> GetStoryAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.GetPublishedStoryAsync(storyId, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetPublishedStoryResponse>> GetStoryAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(storyId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetStoryAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedPagedCollectionOfListPublishedStoriesItem>> ListPublishedStoriesAsync(
        int? page = null,
        int? pageSize = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        string? authorId = null,
        string? completionStatus = null,
        string? tagKey = null,
        CancellationToken cancellationToken = default
    ) => await client.ListPublishedStoriesAsync(page, pageSize, search, sort, fields, authorId, completionStatus, tagKey, cancellationToken).HandleApiException();
}
