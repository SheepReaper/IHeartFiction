using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Features.Authoring;

public class ChapterService(FictionApiClient client)
{
    public async ValueTask<Result<LinkedOfCreateStoryChapterResponse>> AddChapterToStoryAsync(
        Ulid storyId,
        CreateStoryChapterBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.CreateStoryChapterAsync(storyId, body, fields, cancellationToken).HandleApiException();

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
        : await client.DeleteChapterAsync(chapterId, cancellationToken).HandleApiException();

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
        : await client.UpdateChapterMetadataAsync(chapterId, body, fields, cancellationToken).HandleApiException();

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
        : await client.UpdateChapterContentAsync(chapterId, body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateChapterContentResponse>> UpdateChapterContentAsync(
        string chapterId,
        UpdateChapterContentBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateChapterContentAsync(ulid, body, fields, cancellationToken);
}
