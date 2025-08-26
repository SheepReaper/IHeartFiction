using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Infrastructure;

public sealed class CreatedAtInterceptor(TimeProvider dt) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var idEntries = eventData
            .Context
            .ChangeTracker
            .Entries<ICreatedAt>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in idEntries)
        {
            entry.Entity.CreatedAt = dt.GetUtcNow().UtcDateTime;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
