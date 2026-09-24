#pragma warning disable CA1515 // Wolverine discovers public message handlers.

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Stories;

namespace IHFiction.FictionApi.Tags;

public sealed class TagCanonicalReconciliationHandler(FictionDbContext context)
{
    public async Task Handle(TagCreatedRequested message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var createdTag = await context.Tags
            .Include(tag => tag.Works)
            .FirstOrDefaultAsync(tag => tag.Id == message.TagId, cancellationToken);

        if (createdTag is null)
        {
            return;
        }

        var candidateTags = await context.Tags
            .Where(tag => tag.Id != createdTag.Id)
            .Include(tag => tag.Works)
            .OrderBy(tag => tag.CreatedAt)
            .ThenBy(tag => tag.Id)
            .ToListAsync(cancellationToken);

        var duplicates = candidateTags
            .Where(tag =>
                tag.Category == createdTag.Category
                && tag.Subcategory == createdTag.Subcategory
                && TagCanonicalizationService.Matches(
                    tag.Category,
                    tag.Subcategory,
                    tag.Value,
                    createdTag.Category,
                    createdTag.Subcategory,
                    createdTag.Value))
            .ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        var canonicalTag = duplicates
            .Append(createdTag)
            .OrderBy(tag => tag.CreatedAt)
            .ThenBy(tag => tag.Id)
            .First();

        foreach (var duplicateTag in duplicates.Where(tag => tag.Id != canonicalTag.Id).ToArray())
        {
            foreach (var work in duplicateTag.Works.ToArray())
            {
                if (!canonicalTag.Works.Any(candidate => candidate.Id == work.Id))
                {
                    canonicalTag.Works.Add(work);
                }

                work.Tags.Remove(duplicateTag);
            }

            context.Tags.Remove(duplicateTag);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}