using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Services;

public class AuthorService(FictionApiClient client)
{
    public async ValueTask<Result<LinkedOfGetAuthorResponse>> GetAuthorAsync(
        Ulid authorId,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (authorId == Ulid.Empty)
        {
            return DomainError.EmptyUlid;
        }

        try
        {
            return await client.GetAuthorAsync(authorId, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }

    public async ValueTask<Result<LinkedOfGetAuthorResponse>> GetAuthorAsync(
        string authorId,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        return !Ulid.TryParse(authorId, out var ulid)
            ? DomainError.InvalidUlid
            : await GetAuthorAsync(ulid, fields, cancellationToken);
    }

    public async ValueTask<Result<LinkedPagedCollectionOfListAuthorsItem>> ListAuthorsAsync(
        int? page = null,
        int? pageSize = null,
        string? search = null,
        string? sort = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.ListAuthorsAsync(page, pageSize, search, sort, fields, cancellationToken);
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
}
