using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public class WorkService(IFictionApiClient client)
{
    public async ValueTask<Result<LinkedOfPublishWorkResponse>> PublishWorkAsync(
        Ulid id,
        PublishWorkBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => id == Ulid.Empty
        ? DomainError.EmptyUlid
        : await client.PublishWorkAsync(id.ToString(), body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfPublishWorkResponse>> PublishWorkAsync(
        string id,
        PublishWorkBody body,
        string? fields = null,
        CancellationToken cancellationToken = default
    ) => !Ulid.TryParse(id, out var ulid)
        ? DomainError.InvalidUlid
        : await PublishWorkAsync(ulid, body, fields, cancellationToken);
}
