using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class ChapterService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfGetOwnChapterContentResponse>> GetCurrentAuthorChapterContentAsync(
        Ulid chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => chapterId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetOwnChapterContentAsync(chapterId.ToString(), fields, cancellationToken).HandleApiException();
    public async ValueTask<Result<LinkedOfGetOwnChapterContentResponse>> GetCurrentAuthorChapterContentAsync(
        string chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetCurrentAuthorChapterContentAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfGetPublishedChapterContentResponse>> GetChapterContentAsync(
        Ulid chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => chapterId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetPublishedChapterContentAsync(chapterId.ToString(), fields, cancellationToken).HandleApiException();
    public async ValueTask<Result<LinkedOfGetPublishedChapterContentResponse>> GetChapterContentAsync(
        string chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetChapterContentAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfCreateStoryChapterResponse>> AddChapterToStoryAsync(
        Ulid storyId,
        CreateStoryChapterBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.CreateStoryChapterAsync(storyId.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfCreateStoryChapterResponse>> AddChapterToStoryAsync(
        string storyId,
        CreateStoryChapterBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(storyId, out var ulid)
        ? DomainError.InvalidUlid
        : await AddChapterToStoryAsync(ulid, body, fields, cancellationToken);

    public async ValueTask<Result> DeleteChapterAsync(
        Ulid chapterId,
        CancellationToken cancellationToken = default
    ) => chapterId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.DeleteChapterAsync(chapterId.ToString(), cancellationToken).HandleApiException();

    public async ValueTask<Result> DeleteChapterAsync(
        string chapterId,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await DeleteChapterAsync(ulid, cancellationToken);

    public async ValueTask<Result<LinkedOfUpdateChapterMetadataResponse>> UpdateChapterMetadataAsync(
        Ulid chapterId,
        UpdateChapterMetadataBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => chapterId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.UpdateChapterMetadataAsync(chapterId.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateChapterMetadataResponse>> UpdateChapterMetadataAsync(
        string chapterId,
        UpdateChapterMetadataBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateChapterMetadataAsync(ulid, body, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfUpdateChapterContentResponse>> UpdateChapterContentAsync(
        Ulid chapterId,
        UpdateChapterContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => chapterId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.UpdateChapterContentAsync(chapterId.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateChapterContentResponse>> UpdateChapterContentAsync(
        string chapterId,
        UpdateChapterContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateChapterContentAsync(ulid, body, fields, cancellationToken);
}