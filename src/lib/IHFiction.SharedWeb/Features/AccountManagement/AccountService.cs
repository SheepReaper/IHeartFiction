using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Features.AccountManagement;

public class AccountService(FictionApiClient client)
{
    public async ValueTask<Result<LinkedOfRegisterAsAuthorResponse>> RegisterAsAuthorAsync(
        RegisterAsAuthorBody body,
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.RegisterAsAuthorAsync(body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnUserProfileResponse>> GetUserProfileAsync(
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.GetOwnUserProfileAsync(fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateOwnUserProfileResponse>> UpdateUserProfileAsync(
        UpdateOwnUserProfileBody body,
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.UpdateOwnUserProfileAsync(body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfGetOwnAuthorProfileResponse>> GetAuthorProfileAsync(
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.GetOwnAuthorProfileAsync(fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateOwnAuthorProfileResponse>> UpdateAuthorProfileAsync(
        UpdateOwnAuthorProfileBody body,
        string? fields = null,
        CancellationToken cancellationToken = default) => await client.UpdateOwnAuthorProfileAsync(body, fields, cancellationToken).HandleApiException();

    public async ValueTask<Result<LinkedOfUpdateOwnAuthorProfileResponse>> UpdateAuthorBioAsync(
        UpdateOwnAuthorProfileBody body,
        string? fields = null,
        CancellationToken cancellationToken = default) => await UpdateAuthorProfileAsync(body, fields, cancellationToken);
}
