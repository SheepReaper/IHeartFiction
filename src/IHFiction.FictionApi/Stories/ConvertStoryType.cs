using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Stories;


internal sealed class ConvertStoryType(
    FictionDbContext context,
    StoryDbContext storyDbContext,
    EntityLoaderService entityLoader,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<ConvertStoryType>
{
    internal static class Errors
    {
        public static readonly DomainError NotAuthorized = new("ConvertStoryType.NotAuthorized", "You are not authorized to convert this story.");
        public static readonly DomainError InvalidConversionPath = new("ConvertStoryType.InvalidConversionPath", "The requested story type conversion is not valid.");
        public static readonly DomainError DowngradeChapterConditionNotMet = new("ConvertStoryType.DowngradeChapterConditionNotMet", "To downgrade, the story must have exactly one chapter.");
        public static readonly DomainError DowngradeBookConditionNotMet = new("ConvertStoryType.DowngradeBookConditionNotMet", "To downgrade, the story must have exactly one book.");
        public static readonly DomainError UpgradeOneShotConditionNotMet = new("ConvertStoryType.UpgradeOneShotConditionNotMet", "To upgrade, the story must have a WorkBodyId.");
        public static readonly DomainError AlreadyAtTargetType = new("ConvertStoryType.AlreadyAtTargetType", "The story is already of the target type.");
    }

    /// <summary>
    /// Request model for converting a story's type.
    /// </summary>
    /// <param name="TargetType">The new type of the story.</param>
    internal sealed record ConvertStoryTypeBody(
        [property: Required(ErrorMessage = "TargetType is required.")]
        [property: AllowedValues(StoryType.SingleBody, StoryType.MultiChapter, StoryType.MultiBook, ErrorMessage = "Invalid target type.")]
        string? TargetType
        );

    public async Task<Result> HandleAsync(Ulid id, ConvertStoryTypeBody body, ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken)
    {
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;
        var author = authorResult.Value;

        var story = await entityLoader.LoadStoryForConversionAsync(id, cancellationToken);
        if (story is null) return CommonErrors.Story.NotFound;

        if (story.OwnerId != author.Id) return Errors.NotAuthorized;

        var currentType = GetCurrentStoryType(story);
        if (currentType == body.TargetType) return Errors.AlreadyAtTargetType;

        return await PerformConversion(story, currentType == StoryType.New ? StoryType.SingleBody : currentType, body.TargetType!, cancellationToken);
    }

    private static string GetCurrentStoryType(Story story)
    {
        return story switch
        {
            { } when story.HasBooks => StoryType.MultiBook,
            { } when story.HasChapters => StoryType.MultiChapter,
            { } when story.HasContent => StoryType.SingleBody,
            { } when !story.HasContent => StoryType.New,
            _ => StoryType.Unknown
        };
    }

    private async Task<Result> PerformConversion(Story story, string currentType, string targetType, CancellationToken cancellationToken)
    {
        var conversionPath = (currentType, targetType);
        var strategy = context.Database.CreateExecutionStrategy();
        var result = Result.Success();

        Work? modifiedWork = null;

        (Func<Result> converter, Func<CancellationToken, Task<bool>> validator) logic = conversionPath switch
        {
            (StoryType.SingleBody, StoryType.MultiChapter) => (() => UpgradeOneShotToChaptered(story, out modifiedWork), async (ctx) => await context.Stories.Where(s => s.Id == story.Id && s.WorkBodyId == null).AnyAsync(ctx)),
            (StoryType.MultiChapter, StoryType.SingleBody) => (() => DowngradeChapteredToOneShot(story, out modifiedWork), async (ctx) => await context.Stories.Where(s => s.Id == story.Id && s.WorkBodyId != null).AnyAsync(ctx)),
            (StoryType.MultiChapter, StoryType.MultiBook) => (() => UpgradeChapteredToBookBased(story), async (ctx) => await context.Stories.Where(s => s.Id == story.Id && s.Books.Count == 1).AnyAsync(ctx)),
            (StoryType.MultiBook, StoryType.MultiChapter) => (() => DowngradeBookBasedToChaptered(story), async (ctx) => await context.Stories.Where(s => s.Id == story.Id && s.Books.Count == 0).AnyAsync(ctx)),
            _ => (() => Errors.InvalidConversionPath, (_) => Task.FromResult(false))
        };

        var (converter, validator) = logic;

        await strategy.ExecuteInTransactionAsync(async (ctx) =>
        {
            result = converter();

            if (result.IsFailure) throw new InvalidOperationException("Conversion failed. Rolling back transaction.");

            if (modifiedWork is Chapter chapter)
            {
                // If the work body already exists in the story store, don't attempt to add it again,
                // but continue so that relational story changes are saved below. Previously this
                // returned early which skipped saving the relational context and resulted in no
                // updates being persisted.
                var exists = await storyDbContext.WorkBodies.AnyAsync(wb => wb.Id == chapter.WorkBodyId, ctx);
                if (!exists)
                {
                    storyDbContext.WorkBodies.Add(new() { Id = chapter.WorkBodyId, Content = string.Empty });

                    await storyDbContext.SaveChangesAsync(false, ctx);
                }
            }

            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, ctx);
        }, async (ctx) =>
        {
            if (result.IsFailure) return false;

            if (await validator.Invoke(ctx) && (modifiedWork is null || (modifiedWork is Chapter chapter && await storyDbContext.WorkBodies.AnyAsync(wb => wb.Id == chapter.WorkBodyId, ctx)))) return true;

            result = CommonErrors.Database.SaveFailed;

            return false;
        }, cancellationToken);

        if (result.IsFailure) return result;

        context.ChangeTracker.AcceptAllChanges();
        storyDbContext.ChangeTracker.AcceptAllChanges();

        return result;
    }

    private Result UpgradeOneShotToChaptered(Story story, out Work? modifiedWork)
    {
        modifiedWork = null;

        if (story.WorkBodyId is null) return Errors.UpgradeOneShotConditionNotMet;

        var chapter = new Chapter { Title = "Chapter 1", StoryId = story.Id, WorkBodyId = story.WorkBodyId.Value, Owner = story.Owner };

        story.WorkBodyId = null;
        story.Chapters.Add(chapter);
        context.Stories.Update(story);
        context.Chapters.Add(chapter);

        modifiedWork = chapter;

        return Result.Success();
    }

    private Result DowngradeChapteredToOneShot(Story story, out Work? modifiedWork)
    {
        modifiedWork = null;

        if (story.Chapters.Count != 1) return Errors.DowngradeChapterConditionNotMet;

        var chapter = story.Chapters[0];

        story.WorkBodyId = chapter.WorkBodyId;
        // chapter.WorkBodyId = ObjectId.Empty;
        context.Chapters.Remove(chapter);

        modifiedWork = story;

        return Result.Success();
    }

    private Result UpgradeChapteredToBookBased(Story story)
    {
        var book = new Book { Title = "Book 1", StoryId = story.Id, Owner = story.Owner, Description = "Book 1 description" };

        // Ensure owner is also in Authors
        if (!book.Authors.Any(a => a.Id == story.OwnerId))
        {
            book.Authors.Add(story.Owner);
        }

        context.Books.Add(book);

        foreach (var chapter in story.Chapters)
        {
            chapter.Book = book;
            chapter.StoryId = null;
        }

        // story.Chapters.Clear();

        return Result.Success();
    }

    private Result DowngradeBookBasedToChaptered(Story story)
    {
        if (story.Books.Count != 1) return Errors.DowngradeBookConditionNotMet;

        var book = story.Books[0];

        foreach (var chapter in book.Chapters)
        {
            chapter.Book = null;
            chapter.StoryId = story.Id;
        }

        context.Books.Remove(book);

        return Result.Success();
    }
    public static string EndpointName => nameof(ConvertStoryType);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/convert",
                async (
                    [FromRoute] Ulid id,
                    [FromBody] ConvertStoryTypeBody body,
                    ConvertStoryType useCase,
                    ClaimsPrincipal claimsPrincipal,
                    CancellationToken cancellationToken) =>
                {
                    var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                    return result.ToOkResult();
                })
                .WithSummary("Convert Story Type")
                .WithDescription("Converts a story from one type to another (e.g., One-Shot to Chaptered). The conversion path is linear and has conditions for downgrading.")
                .WithTags(ApiTags.Stories.Management)
                .RequireAuthorization("author")
                .WithStandardResponses()
                .Accepts<ConvertStoryTypeBody>(MediaTypeNames.Application.Json);
        }
    }
}
