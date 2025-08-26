using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Infrastructure;

public sealed class UpdatedAtInterceptor(TimeProvider dt) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var newEntries = eventData
            .Context
            .ChangeTracker
            .Entries<IUpdatedAt>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in newEntries)
        {
            entry.Entity.UpdatedAt = dt.GetUtcNow().UtcDateTime;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
