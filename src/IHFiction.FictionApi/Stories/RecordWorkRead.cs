#pragma warning disable CA1515 // Wolverine discovers public message types.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

using Microsoft.AspNetCore.Mvc;

using Wolverine;

namespace IHFiction.FictionApi.Stories;

public sealed record RecordWorkReadRequested(
    Ulid WorkId,
    Guid? AuthenticatedUserId,
    string? DeviceId,
    DateTime QualifiedAt = default);

internal sealed class RecordWorkRead(IMessageBus messageBus, TimeProvider timeProvider) : IUseCase, INameEndpoint<RecordWorkRead>
{
    internal sealed record RecordWorkReadBody(
        [property: Range(10, 3600)] int ActiveSeconds,
        bool HasMeaningfulInteraction);

    internal sealed record RecordWorkReadResponse(Ulid WorkId, bool Qualified);

    internal static class Errors
    {
        public static readonly DomainError NotQualified = new("RecordWorkRead.NotQualified", "The reading session has not met the qualification threshold.");
        public static readonly DomainError DeviceRequired = new("RecordWorkRead.DeviceRequired", "A valid device identifier is required for anonymous readers.");
        public static readonly DomainError NotDirectlyReadable = new("RecordWorkRead.NotDirectlyReadable", "The work is not directly readable.");
        public static readonly DomainError WorkNotFound = new("RecordWorkRead.NotFound", "Work not found.");
    }

    public async Task<Result<RecordWorkReadResponse>> HandleAsync(
        Ulid id,
        RecordWorkReadBody body,
        ClaimsPrincipal principal,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (body.ActiveSeconds < 10 || !body.HasMeaningfulInteraction) return Errors.NotQualified;

        Guid? authenticatedUserId = null;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var userIdResult = principal.GetUid();
            if (userIdResult.IsFailure) return userIdResult.DomainError;
            authenticatedUserId = userIdResult.Value;
        }

        if (authenticatedUserId is null && !DeviceIdHeader.IsValid(deviceId)) return Errors.DeviceRequired;
        if (deviceId is not null && !DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

        await messageBus.PublishAsync(new RecordWorkReadRequested(
            id,
            authenticatedUserId,
            deviceId,
            timeProvider.GetUtcNow().UtcDateTime));

        return new RecordWorkReadResponse(id, true);
    }

    public static string EndpointName => nameof(RecordWorkRead);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder) => builder
            .MapPost("works/{id:ulid}/reads", async (
                [FromRoute] Ulid id,
                [FromBody] RecordWorkReadBody body,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                ClaimsPrincipal principal,
                RecordWorkRead useCase,
                CancellationToken cancellationToken) =>
                (await useCase.HandleAsync(id, body, principal, deviceId, cancellationToken))
                    .ToResult(accepted => Results.Accepted(value: accepted)))
            .WithSummary("Record a qualified work read")
            .WithDescription("Durably queues a qualified unique read for asynchronous recording against a story or chapter and its parent works.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false)
            .RequireRateLimiting("qualified-reads")
            .Produces<RecordWorkReadResponse>(StatusCodes.Status202Accepted);
    }
}
