using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class BookService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfGetOwnBookContentResponse>> GetCurrentAuthorBookContentAsync(
        Ulid bookId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => bookId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetOwnBookContentAsync(bookId.ToString(), fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnBookContentResponse>> GetCurrentAuthorBookContentAsync(
        string bookId,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(bookId, out var ulid)
        ? DomainError.InvalidUlid
        : await GetCurrentAuthorBookContentAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfUpdateBookMetadataResponse>> UpdateBookMetadataAsync(
        Ulid bookId,
        UpdateBookMetadataBody body,
        CancellationToken cancellationToken = default
    ) => bookId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.UpdateBookMetadataAsync(bookId.ToString(), body, null, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateBookMetadataResponse>> UpdateBookMetadataAsync(
        string bookId,
        UpdateBookMetadataBody body,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(bookId, out var ulid)
        ? DomainError.InvalidUlid
        : await UpdateBookMetadataAsync(ulid, body, cancellationToken);

    public async ValueTask<Result<LinkedOfCreateBookChapterResponse>> AddChapterToBookAsync(
        Ulid bookId,
        CreateBookChapterBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => bookId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.CreateBookChapterAsync(bookId.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfCreateBookChapterResponse>> AddChapterToBookAsync(
        string bookId,
        CreateBookChapterBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(bookId, out var ulid)
        ? DomainError.InvalidUlid
        : await AddChapterToBookAsync(ulid, body, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfCreateBookResponse>> CreateBookAsync(
        Ulid storyId,
        CreateBookBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => storyId == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.CreateBookAsync(storyId.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfCreateBookResponse>> CreateBookAsync(
        string storyId,
        CreateBookBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(storyId, out var ulid)
        ? DomainError.InvalidUlid
        : await CreateBookAsync(ulid, body, fields, cancellationToken);
}
