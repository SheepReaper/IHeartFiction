using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class AccountService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfRegisterAsAuthorResponse>> RegisterAsAuthorAsync(
        RegisterAsAuthorBody body,
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.RegisterAsAuthorAsync(body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedPagedCollectionOfAuthorStoryItem>> GetCurrentAuthorStoriesAsync(
        GetOwnStoriesBody body,
        int? pageSize = null,
        int? page = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.GetOwnStoriesAsync(pageSize, page, search, sort, fields, body, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnStoryContentResponse>> GetCurrentAuthorStoryContentAsync(
        Ulid storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetOwnStoryContentAsync(storyId.ToString(), fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnStoryContentResponse>> GetCurrentAuthorStoryContentAsync(
        string storyId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(storyId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetCurrentAuthorStoryContentAsync(ulid, fields, cancellationToken);

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
}