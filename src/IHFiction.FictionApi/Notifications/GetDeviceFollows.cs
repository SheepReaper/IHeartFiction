using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Notifications;

internal sealed class GetDeviceFollows(FictionDbContext context) : IUseCase, INameEndpoint<GetDeviceFollows>
{
    internal sealed record GetDeviceFollowsQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetDeviceFollowsResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record DeviceFollowedAuthorItem(Ulid AuthorId, string Name);
    internal sealed record DeviceFollowedStoryItem(Ulid StoryId, string Title, bool IsPublished);
    internal sealed record GetDeviceFollowsResponse(
        IEnumerable<DeviceFollowedAuthorItem> Authors,
        IEnumerable<DeviceFollowedStoryItem> Stories);

    public async Task<Result<GetDeviceFollowsResponse>> HandleAsync(
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

        var authors = await context.DeviceAuthorFollows
            .AsNoTracking()
            .Where(follow => follow.DeviceId == deviceId)
            .Join(
                context.Authors.AsNoTracking(),
                follow => follow.AuthorId,
                author => author.Id,
                (follow, author) => new { author.Id, author.Name })
            .OrderBy(item => item.Name)
            .Select(item => new DeviceFollowedAuthorItem(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var stories = await context.DeviceStoryFollows
            .AsNoTracking()
            .Where(follow => follow.DeviceId == deviceId)
            .Join(
                context.Stories.AsNoTracking(),
                follow => follow.StoryId,
                story => story.Id,
                (follow, story) => new { story.Id, story.Title, IsPublished = story.PublishedAt != null })
            .OrderBy(item => item.Title)
            .Select(item => new DeviceFollowedStoryItem(item.Id, item.Title, item.IsPublished))
            .ToListAsync(cancellationToken);

        return new GetDeviceFollowsResponse(authors, stories);
    }

    public static string EndpointName => nameof(GetDeviceFollows);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("device/follows", async (
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] GetDeviceFollowsQuery query,
                GetDeviceFollows useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(deviceId, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Get Device Follows")
            .WithDescription("Lists the authors and stories followed by an anonymous browser or installed PWA device.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<GetDeviceFollowsResponse>();
        }
    }
}
