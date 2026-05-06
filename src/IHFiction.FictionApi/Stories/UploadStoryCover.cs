using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Stories;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;

namespace IHFiction.FictionApi.Stories;

internal sealed class UploadStoryCover(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<UploadStoryCover>
{
    internal static class Errors
    {
        public static readonly Error EmptyFile = new("UploadStoryCover.EmptyFile", "A cover image file is required.");
        public static readonly Error UnsupportedContentType = new("UploadStoryCover.UnsupportedContentType", "Cover images must be JPG, PNG, or WebP.");
        public static readonly Error FileTooLarge = new("UploadStoryCover.FileTooLarge", $"Cover images must be {StoryCoverRules.MaxFileSizeBytes / (1024 * 1024)} MB or smaller.");
        public static readonly Error DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly Error ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
    }

    internal sealed record UploadStoryCoverResponse(
        Ulid StoryId,
        bool HasCoverImage,
        DateTime UpdatedAt);

    public async Task<Result<UploadStoryCoverResponse>> HandleAsync(
        Ulid id,
        IFormFile file,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Errors.EmptyFile;
        }

        if (file.Length > StoryCoverRules.MaxFileSizeBytes)
        {
            return Errors.FileTooLarge;
        }

        if (!StoryCoverRules.IsAllowedContentType(file.ContentType)
            || !StoryCoverRules.IsAllowedFileExtension(file.FileName))
        {
            return Errors.UnsupportedContentType;
        }

        var authResult = await authorizationService.AuthorizeStoryAccessAsync(
            id,
            claimsPrincipal,
            StoryAccessLevel.Edit,
            cancellationToken: cancellationToken);

        if (authResult.IsFailure)
        {
            return authResult.DomainError;
        }

        var (story, _, _) = authResult.Value;

        byte[] content;
        await using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream, cancellationToken);
            content = memoryStream.ToArray();
        }

        var existingCover = await context.StoryCovers
            .SingleOrDefaultAsync(cover => cover.StoryId == story.Id, cancellationToken);

        if (existingCover is null)
        {
            existingCover = new StoryCover
            {
                StoryId = story.Id,
                Story = story
            };

            context.StoryCovers.Add(existingCover);
        }

        existingCover.OriginalFileName = Path.GetFileName(file.FileName);
        existingCover.ContentType = file.ContentType;
        existingCover.FileSizeBytes = content.LongLength;
        existingCover.ContentHash = Convert.ToHexString(SHA256.HashData(content));
        existingCover.Content = content;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Errors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }

        return new UploadStoryCoverResponse(story.Id, true, existingCover.UpdatedAt);
    }

    public static string EndpointName => nameof(UploadStoryCover);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("stories/{id:ulid}/cover", async (
                [FromRoute] Ulid id,
                [FromForm] IFormFile file,
                UploadStoryCover useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, file, claimsPrincipal, cancellationToken);

                return result.ToOkResult();
            })
            .WithSummary("Upload Story Cover")
            .WithDescription("Uploads or replaces the cover image for a story. Only the story owner or authorized collaborators can update the cover image. Requires authentication.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(validation: false)
            .Produces<Linked<UploadStoryCoverResponse>>()
            .Accepts<IFormFile>(MediaTypeNames.Multipart.FormData)
            .DisableAntiforgery();
        }
    }
}