using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class WorkService(FictionApiClient client)
{
    public async ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
        Ulid id,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetPublishedWorkMetaAsync(id, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
        string id,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await GetPublishedWorkMetaAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
        Ulid id,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.GetPublishedWorkContentAsync(id, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
        string id,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await GetPublishedWorkContentAsync(ulid, fields, cancellationToken);

    public async ValueTask<Result<LinkedOfPublishWorkResponse>> PublishWorkAsync(
        Ulid id,
        PublishWorkBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.PublishWorkAsync(id, body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfPublishWorkResponse>> PublishWorkAsync(
        string id,
        PublishWorkBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await PublishWorkAsync(ulid, body, fields, cancellationToken);
}
