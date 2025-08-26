using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Authors;

internal sealed class GetAuthorById(FictionDbContext context) : IUseCase, INameEndpoint<GetAuthorById>
{
    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetAuthorByIdResponse>]
        [property: DataMember(IsRequired = false)]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents an author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    internal sealed record AuthorProfile(string? Bio);

    /// <summary>
    /// Represents a work (story) created by an author.
    /// </summary>
    /// <param name="Id">Unique identifier for the work</param>
    /// <param name="Title">Title of the work</param>
    internal sealed record AuthorWorkItem(Ulid Id, string Title);

    /// <summary>
    /// Response model for getting a specific author by their ID.
    /// </summary>
    /// <param name="UserId">The user ID associated with this author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="DeletedAt">When the author was deleted (null if not deleted)</param>
    /// <param name="Profile">Author's profile information including bio</param>
    /// <param name="Works">Collection of works created by this author</param>
    internal sealed record GetAuthorByIdResponse(
        Guid UserId,
        string Name,
        DateTime UpdatedAt,
        DateTime? DeletedAt,
        AuthorProfile Profile,
        IEnumerable<AuthorWorkItem> Works);
    public async Task<Result<GetAuthorByIdResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        var author = await context.Authors
            .Include(a => a.Profile)
            .Include(a => a.Works)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return author is null
            ? CommonErrors.Author.NotFound
            : new GetAuthorByIdResponse(
                author.UserId,
                author.Name,
                author.UpdatedAt,
                author.DeletedAt,
                new AuthorProfile(author.Profile.Bio),
                author.Works.Select(work => new AuthorWorkItem(work.Id, work.Title)));
    }
    public static string EndpointName => nameof(GetAuthorById);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;
        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("authors/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                GetAuthorById useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result
                    .WithLinks([
                        linker.Create<GetAuthorById>("self", HttpMethods.Get, new { id }),
                        linker.Create<UpdateAuthorProfile>("update-profile", HttpMethods.Get, new { id })])
                    .ToOkResult(query);
            })
            .WithSummary("Get Author by ID")
            .WithDescription("Retrieves detailed information about a specific author by their unique identifier. " +
                "Returns the author's profile information and a list of their works. " +
                "This is a public endpoint that does not require authentication.")
            .WithTags(ApiTags.Authors.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<GetAuthorByIdResponse>>();
        }
    }
}
