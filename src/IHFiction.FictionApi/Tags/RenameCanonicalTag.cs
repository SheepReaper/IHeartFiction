using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Tags;

internal sealed partial class RenameCanonicalTag(
    FictionDbContext context,
    ILogger<RenameCanonicalTag> logger) : IUseCase, INameEndpoint<RenameCanonicalTag>
{
    internal static class Errors
    {
        public static readonly DomainError NotFound = new("RenameCanonicalTag.NotFound", "Canonical tag not found.");
        public static readonly DomainError Conflict = new("RenameCanonicalTag.Conflict", "That tag spelling already belongs to another tag family.");
        public static readonly DomainError InvalidReason = new("RenameCanonicalTag.InvalidReason", "A meaningful administrative reason is required.");
        public static readonly DomainError InvalidTag = new("RenameCanonicalTag.InvalidTag", "Category and value are required.");
    }

    internal sealed record RenameCanonicalTagBody(
        [property: Required, StringLength(50, MinimumLength = 1)] string Category,
        [property: StringLength(50)] string? Subcategory,
        [property: Required, StringLength(50, MinimumLength = 1)] string Value,
        [property: Required, StringLength(500, MinimumLength = 3)] string Reason);

    internal sealed record RenameCanonicalTagResponse(
        Ulid TagId,
        string Category,
        string? Subcategory,
        string Value,
        string DisplayFormat,
        IReadOnlyCollection<string> Synonyms);

    public async Task<Result<RenameCanonicalTagResponse>> HandleAsync(
        Ulid id,
        RenameCanonicalTagBody body,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Trim().Length < 3) return Errors.InvalidReason;

        var category = InputSanitizationService.SanitizeText(body.Category);
        var subcategory = string.IsNullOrWhiteSpace(body.Subcategory)
            ? null
            : InputSanitizationService.SanitizeText(body.Subcategory);
        var value = InputSanitizationService.SanitizeText(body.Value);
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(value)) return Errors.InvalidTag;
        var normalizedKey = Tag.BuildNormalizedKey(category, subcategory, value);

        if (!await context.Tags.OfType<CanonicalTag>().AnyAsync(candidate => candidate.Id == id, cancellationToken))
        {
            return Errors.NotFound;
        }

        if (await context.Tags.AnyAsync(candidate => candidate.Id != id && candidate.NormalizedKey == normalizedKey, cancellationToken))
        {
            return Errors.Conflict;
        }

        RenameCanonicalTagResponse? response = null;
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            context.ChangeTracker.Clear();
            var tag = await context.Tags.OfType<CanonicalTag>()
                .Include(candidate => candidate.Synonyms)
                .SingleAsync(candidate => candidate.Id == id, transactionCancellationToken);
            var oldCategory = tag.Category;
            var oldSubcategory = tag.Subcategory;
            var oldValue = tag.Value;
            var oldKey = tag.NormalizedKey;

            tag.Rename(category, subcategory, value);
            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, transactionCancellationToken);
            context.ChangeTracker.AcceptAllChanges();

            if (!string.Equals(oldKey, normalizedKey, StringComparison.Ordinal))
            {
                var synonym = Tag.CreateSynonym(tag, oldCategory, oldSubcategory, oldValue);
                context.Tags.Add(synonym);
                await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, transactionCancellationToken);
                context.ChangeTracker.AcceptAllChanges();
                response = ToResponse(tag) with { Synonyms = [.. tag.Synonyms.Select(item => item.ToString()), synonym.ToString()] };
            }
            else
            {
                response = ToResponse(tag);
            }
        }, async transactionCancellationToken =>
            await context.Tags.OfType<CanonicalTag>().AnyAsync(
                candidate => candidate.Id == id && candidate.NormalizedKey == normalizedKey,
                transactionCancellationToken), cancellationToken);

        context.ChangeTracker.AcceptAllChanges();

        LogRenamed(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", id, body.Reason.Trim());

        return response!;
    }

    private static RenameCanonicalTagResponse ToResponse(CanonicalTag tag) => new(
        tag.Id,
        tag.Category,
        tag.Subcategory,
        tag.Value,
        tag.ToString(),
        [.. tag.Synonyms.Select(synonym => synonym.ToString())]);

    [LoggerMessage(LogLevel.Information, "Administrator {AdministratorId} renamed canonical tag {TagId}. Reason: {Reason}")]
    private partial void LogRenamed(string administratorId, Ulid tagId, string reason);

    public static string EndpointName => nameof(RenameCanonicalTag);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder) => builder
            .MapPut("admin/tags/{id:ulid}", async (
                [FromRoute] Ulid id,
                [FromBody] RenameCanonicalTagBody body,
                RenameCanonicalTag useCase,
                ClaimsPrincipal principal,
                CancellationToken cancellationToken) =>
                (await useCase.HandleAsync(id, body, principal, cancellationToken)).ToOkResult())
            .WithSummary("Rename Canonical Tag")
            .WithDescription("Renames a canonical tag while retaining its previous spelling as a synonym. Requires an administrative reason.")
            .WithTags(ApiTags.Tags.Administration)
            .RequireAuthorization("admin")
            .WithStandardResponses()
            .Produces<RenameCanonicalTagResponse>();
    }
}
