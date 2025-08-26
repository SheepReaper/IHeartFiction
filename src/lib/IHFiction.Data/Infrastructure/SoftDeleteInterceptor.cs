using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Infrastructure;

public sealed class SoftDeleteInterceptor(TimeProvider dt) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var deletableEntries = eventData
            .Context
            .ChangeTracker
            .Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted);

        foreach (var entry in deletableEntries)
        {
            entry.State = EntityState.Modified;
            entry.Entity.DeletedAt = dt.GetUtcNow().UtcDateTime;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
