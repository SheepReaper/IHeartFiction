using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Account;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Authors;

internal sealed class GetAuthor(FictionDbContext context) : IUseCase, INameEndpoint<GetAuthor>
{
    internal sealed record GetAuthorQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetAuthorResponse>]
        [property: DataMember(IsRequired = false)]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents an author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    internal sealed record GaAuthorProfile(string? Bio);

    /// <summary>
    /// Represents a work (story) created by an author.
    /// </summary>
    /// <param name="Id">Unique identifier for the work</param>
    /// <param name="Title">Title of the work</param>
    /// <param name="PublishedAt">When the work was published (null if unpublished)</param>
    internal sealed record AuthorWorkItem(Ulid Id, string Title, DateTime? PublishedAt);

    /// <summary>
    /// Response model for getting a specific author by their ID.
    /// </summary>
    /// <param name="UserId">The user ID associated with this author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="DeletedAt">When the author was deleted (null if not deleted)</param>
    /// <param name="Profile">Author's profile information including bio</param>
    /// <param name="PublishedStories">Collection of works created by this author</param>
    /// <param name="TotalStories">Total number of stories by the author</param>
    internal sealed record GetAuthorResponse(
        Guid UserId,
        string Name,
        DateTime UpdatedAt,
        DateTime? DeletedAt,
        GaAuthorProfile Profile,
        IEnumerable<AuthorWorkItem> PublishedStories,
        int TotalStories);
    public async Task<Result<GetAuthorResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        var author = await context.Authors
            .Include(a => a.Profile)
            .Include(a => a.Works)
            .Where(a => a.Id == id)
            .Select(a => new GetAuthorResponse(
                a.UserId,
                a.Name,
                a.UpdatedAt,
                a.DeletedAt,
                new GaAuthorProfile(a.Profile.Bio),
                a.Works
                    .Where(w => w is Story && w.PublishedAt != null)
                    .Select(w => new AuthorWorkItem(
                        w.Id,
                        w.Title,
                        w.PublishedAt)),
                a.Works.Count(w => w is Story)
            ))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return author is null
            ? CommonErrors.Author.NotFound
            : author;
    }
    public static string EndpointName => nameof(GetAuthor);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;
        
        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("authors/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] GetAuthorQuery query,
                GetAuthor useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                var okResult = result
                    .WithLinks([
                        linker.Create<GetAuthor>("self", HttpMethods.Get, new { id }),
                        linker.Create<UpdateOwnAuthorProfile>("update-profile", HttpMethods.Get, new { id })])
                    .ToOkResult(query);

                return okResult;
            })
            .WithSummary("Get Author by ID")
            .WithDescription("Retrieves detailed information about a specific author by their unique identifier. " +
                "Returns the author's profile information and a list of their works. " +
                "This is a public endpoint that does not require authentication.")
            .WithTags(ApiTags.Authors.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<GetAuthorResponse>>();
        }
    }
}
