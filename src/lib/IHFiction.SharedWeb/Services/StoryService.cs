using Microsoft.Extensions.Logging;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class StoryService(FictionApiClient client, ILogger<StoryService> logger)
{
    private static readonly Action<ILogger, Ulid, Exception?> LogUploadStoryCoverFailed =
        LoggerMessage.Define<Ulid>(
            LogLevel.Error,
            new EventId(1001, nameof(UploadStoryCoverAsync)),
            "Failed to upload story cover for story {StoryId}");

    public async ValueTask<Result> PublishWorkAsync(
        Ulid id,
        bool publishAll = false,
        CancellationToken cancellationToken = default) => id == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.PublishWorkAsync(id, new() { PublishAll = publishAll }, null, cancellationToken).HandleApiException();

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
            : await client.ConvertStoryTypeAsync(id, body, cancellationToken).HandleApiException();

    public async ValueTask<Result> ConvertStoryTypeAsync(
        string id,
        ConvertStoryTypeBody body,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await ConvertStoryTypeAsync(ulid, body, cancellationToken);


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


    public async ValueTask<Result<LinkedOfGetPublishedStoryContentResponse>> GetPublishedStoryContentAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.GetPublishedStoryContentAsync(storyId, fields, cancellationToken).HandleApiException();

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
        string? authorId = null,
        CancellationToken cancellationToken = default
    ) => await client.ListPublishedStoriesAsync(page, pageSize, search, sort, fields, authorId, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedPagedCollectionOfAuthorStoryItem>> GetCurrentAuthorStoriesAsync(
        GetOwnStoriesBody body,
        int? pageSize = null,
        int? page = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => await client.GetOwnStoriesAsync(pageSize, page, search, sort, fields, body, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUnpublishStoryResponse>> UnpublishStoryAsync(
        Ulid id,
        string? fields = null,
        CancellationToken cancellationToken = default
    )
    {
        return id == Ulid.Empty
            ? DomainError.EmptyUlid
            : await client.UnpublishStoryAsync(id, fields, cancellationToken).HandleApiException();
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
        : await client.DeleteStoryAsync(id, cancellationToken).HandleApiException();

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
        : await client.UpdateStoryMetadataAsync(id, body, fields, cancellationToken).HandleApiException();

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
        : await client.UpdateStoryContentAsync(id, body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateStoryContentResponse>> UpdateStoryContentAsync(
        string id,
        UpdateStoryContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateStoryContentAsync(ulid, body, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfDeleteStoryCoverResponse>> DeleteStoryCoverAsync(
        Ulid id,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.DeleteStoryCoverAsync(id, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUploadStoryCoverResponse>> UploadStoryCoverAsync(
        Ulid id,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (id == Ulid.Empty)
        {
            return DomainError.EmptyUlid;
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        try
        {
            return await client.UploadStoryCoverAsync(
                id,
                new(content, fileName, contentType),
                cancellationToken).HandleApiException();
        }
        catch (Exception ex) when (ex is HttpRequestException
            or IOException
            or InvalidOperationException
            or FormatException
            or NotSupportedException
            or ObjectDisposedException
            or TaskCanceledException)
        {
            LogUploadStoryCoverFailed(logger, id, ex);
            return new DomainError("StoryService.UploadStoryCover", $"Failed to upload story cover: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
