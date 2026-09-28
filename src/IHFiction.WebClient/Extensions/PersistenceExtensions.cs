using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

using IHFiction.Data;
using IHFiction.Data.Contexts;

namespace IHFiction.WebClient.Extensions;

internal static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistenceServices(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<FictionDbContext>(
            "fiction-db",
            configureDbContextOptions: (options) => options
                .UseNpgsql(options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
                .UseSnakeCaseNamingConvention());

        var dataProtection = builder.Services.AddDataProtection()
            .SetApplicationName("IHFiction.WebClient");

        if (builder.Environment.IsProduction())
        {
            dataProtection.PersistKeysToDbContext<FictionDbContext>();
        }
        else
        {
            var userDataDirectory = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userDataDirectory))
                throw new InvalidOperationException("A user data directory is required to persist Data Protection keys.");

            var keyDirectory = OperatingSystem.IsWindows()
                ? new DirectoryInfo(Path.Combine(userDataDirectory, "ASP.NET", "DataProtection-Keys"))
                : new DirectoryInfo(Path.Combine(userDataDirectory, ".aspnet", "DataProtection-Keys"));
            dataProtection.PersistKeysToFileSystem(keyDirectory);

            if (OperatingSystem.IsWindows())
                dataProtection.ProtectKeysWithDpapi();
        }

        return builder;
    }
}
