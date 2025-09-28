using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class StoryService(IFictionApiClient client)
{
    public async ValueTask<Result> PublishWorkAsync(
        Ulid id,
        bool publishAll = false,
        CancellationToken cancellationToken = default) => id == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.PublishWorkAsync(id.ToString(), new() { PublishAll = publishAll }, null, cancellationToken).HandleApiException();

    public async ValueTask<Result> PublishWorkAsync(
        string id,
        bool publishAll = false,
        CancellationToken cancellationToken = default)
        => !Ulid.TryParse(id, out var ulid)
            ? DomainError.InvalidUlid
            : await PublishWorkAsync(ulid, publishAll, cancellationToken);


    public async ValueTask<Result> ConvertStoryTypeAsync(
        Ulid id,
        ConvertStoryTypeBody body,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.ConvertStoryTypeAsync(id.ToString(), body, cancellationToken).HandleApiException();

    public async ValueTask<Result> ConvertStoryTypeAsync(
        string id,
        ConvertStoryTypeBody body,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await ConvertStoryTypeAsync(ulid, body, cancellationToken);


    public async ValueTask<Result<LinkedPagedCollectionOfListStoryChaptersItem>> ListStoryChaptersAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default) => storyId == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.ListStoryChaptersAsync(storyId.ToString(), fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedPagedCollectionOfListStoryChaptersItem>> ListStoryChaptersAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default)
        => !Ulid.TryParse(storyId, out var ulid)
            ? DomainError.InvalidUlid
            : await ListStoryChaptersAsync(ulid, fields, cancellationToken);


    public async ValueTask<Result<LinkedOfGetPublishedStoryResponse>> GetStoryByIdAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.GetPublishedStoryAsync(storyId.ToString(), fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetPublishedStoryResponse>> GetStoryByIdAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(storyId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetStoryByIdAsync(ulid, fields, cancellationToken);


    public async ValueTask<Result<LinkedOfGetPublishedStoryContentResponse>> GetPublishedStoryContentAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.GetPublishedStoryContentAsync(storyId.ToString(), fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetPublishedStoryContentResponse>> GetPublishedStoryContentAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(storyId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetPublishedStoryContentAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedPagedCollectionOfListPublishedStoriesItem>> ListPublishedStoriesAsync(
        int? page = null,
        int? pageSize = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        ListPublishedStoriesBody? body = null,
        CancellationToken cancellationToken = default
    ) => await client.ListPublishedStoriesAsync(page, pageSize, search, sort, fields, body, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedPagedCollectionOfAuthorStoryItem>> GetCurrentAuthorStoriesAsync(
        GetCurrentAuthorStoriesBody body,
        int? pageSize = null,
        int? page = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => await client.GetCurrentAuthorStoriesAsync(pageSize, page, search, sort, fields, body, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfPublishStoryResponse>> PublishStoryAsync(
        Ulid id,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        return id == Ulid.Empty
            ? (Result<LinkedOfPublishStoryResponse>)DomainError.EmptyUlid
            : await client.PublishStoryAsync(id.ToString(), fields, cancellationToken).HandleApiException();
    }

    public async ValueTask<Result<LinkedOfPublishStoryResponse>> PublishStoryAsync(
        string id,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await PublishStoryAsync(ulid, fields, cancellationToken);


    public async ValueTask<Result<LinkedOfUnpublishStoryResponse>> UnpublishStoryAsync(
        Ulid id,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        return id == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.UnpublishStoryAsync(id.ToString(), fields, cancellationToken).HandleApiException();
    }

    public async ValueTask<Result<LinkedOfUnpublishStoryResponse>> UnpublishStoryAsync(
        string id,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await UnpublishStoryAsync(ulid, fields, cancellationToken);


    public async ValueTask<Result> DeleteStoryAsync(
        Ulid id,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.DeleteStoryAsync(id.ToString(), cancellationToken).HandleApiException();

    public async ValueTask<Result> DeleteStoryAsync(
        string id,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await DeleteStoryAsync(ulid, cancellationToken);

    public async ValueTask<Result<LinkedOfCreateStoryResponse>> CreateStoryAsync(
        CreateStoryBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => await client.CreateStoryAsync(body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateStoryMetadataResponse>> UpdateStoryMetadataAsync(
        Ulid id,
        UpdateStoryMetadataBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.UpdateStoryMetadataAsync(id.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateStoryMetadataResponse>> UpdateStoryMetadataAsync(
        string id,
        UpdateStoryMetadataBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateStoryMetadataAsync(ulid, body, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfUpdateStoryContentResponse>> UpdateStoryContentAsync(
        Ulid id,
        UpdateStoryContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.UpdateStoryContentAsync(id.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateStoryContentResponse>> UpdateStoryContentAsync(
        string id,
        UpdateStoryContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateStoryContentAsync(ulid, body, fields, cancellationToken);
}
