using System.Security.Claims;

using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Extensions;

internal static class ClaimsPrincipalExtensions
{
    internal static class Errors
    {
        public static readonly DomainError MissingClaim = new("ClaimsPrincipal.MissingClaim", "The NameIdentifier claim is missing from the principal.");
        public static readonly DomainError UnparsableId = new("ClaimsPrincipal.UnparsableId", "The NameIdentifier claim could not be parsed to a GUID.");
        public static readonly DomainError NameNotMapped = new("ClaimsPrincipal.NameNotMapped", "The Name claim is not mapped.");
    }
    public static Result<Guid> GetUid(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return userId is null ? Errors.MissingClaim : ParseGuid(userId);
    }

    private static Result<Guid> ParseGuid(string? userId)
    {
        return Guid.TryParse(userId, out var userGuid) ? userGuid : Errors.UnparsableId;
    }

    public static Result<string> GetUsername(this ClaimsPrincipal principal)
    {
        var username = principal.Identity?.Name;

        return username is null ? Errors.NameNotMapped : username;
    }
}
