using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Infrastructure;

namespace IHFiction.Data;

public static class Extensions
{
    public static DbContextOptionsBuilder WithDefaultInterceptors(this DbContextOptionsBuilder options, TimeProvider? dateTime = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        dateTime ??= TimeProvider.System;

        return options.AddInterceptors(
            new SoftDeleteInterceptor(dateTime),
            new UpdatedAtInterceptor(dateTime),
            new CreatedAtInterceptor(dateTime)
        );
    }
}