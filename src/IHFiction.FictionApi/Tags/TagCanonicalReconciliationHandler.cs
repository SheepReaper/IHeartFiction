#pragma warning disable CA1515 // Wolverine discovers public message handlers.

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
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

        if (createdTag is SynonymTag)
        {
            return;
        }

        var candidateTags = await context.Tags.OfType<CanonicalTag>()
            .Where(tag => tag.Id != createdTag.Id && tag.NormalizedKey == createdTag.NormalizedKey)
            .Include(tag => tag.Synonyms)
            .Include(tag => tag.Works)
            .OrderBy(tag => tag.CreatedAt)
            .ThenBy(tag => tag.Id)
            .ToListAsync(cancellationToken);

        var duplicates = candidateTags.ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        var canonicalTag = duplicates
            .Append((CanonicalTag)createdTag)
            .OrderBy(tag => tag.CreatedAt)
            .ThenBy(tag => tag.Id)
            .First();

        foreach (var duplicateTag in duplicates.Append((CanonicalTag)createdTag).Where(tag => tag.Id != canonicalTag.Id).ToArray())
        {
            foreach (var work in duplicateTag.Works.ToArray())
            {
                if (!canonicalTag.Works.Any(candidate => candidate.Id == work.Id))
                {
                    canonicalTag.Works.Add(work);
                }

                work.Tags.Remove(duplicateTag);
            }

            foreach (var synonym in duplicateTag.Synonyms)
            {
                synonym.CanonicalTag = canonicalTag;
                synonym.CanonicalTagId = canonicalTag.Id;
            }

            var category = duplicateTag.Category;
            var subcategory = duplicateTag.Subcategory;
            var value = duplicateTag.Value;

            await context.SaveChangesAsync(cancellationToken);
            context.Tags.Remove(duplicateTag);
            await context.SaveChangesAsync(cancellationToken);
            context.Tags.Add(Tag.CreateSynonym(canonicalTag, category, subcategory, value));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
