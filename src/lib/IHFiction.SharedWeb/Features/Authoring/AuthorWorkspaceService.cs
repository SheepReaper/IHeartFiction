using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Features.Authoring;

public class AuthorWorkspaceService(FictionApiClient client)
{
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
        : await client.GetOwnStoryContentAsync(storyId, fields, cancellationToken).HandleApiException();

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
        : await client.GetOwnChapterContentAsync(chapterId, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnChapterContentResponse>> GetCurrentAuthorChapterContentAsync(
        string chapterId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(chapterId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetCurrentAuthorChapterContentAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfGetOwnBookContentResponse>> GetCurrentAuthorBookContentAsync(
        Ulid bookId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => bookId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetOwnBookContentAsync(bookId, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnBookContentResponse>> GetCurrentAuthorBookContentAsync(
        string bookId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(bookId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetCurrentAuthorBookContentAsync(ulid, fields, cancellationToken);
}
