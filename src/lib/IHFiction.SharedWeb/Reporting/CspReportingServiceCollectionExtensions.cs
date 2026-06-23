using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.SharedWeb.Reporting;

public static class CspReportingServiceCollectionExtensions
{
    public static IServiceCollection AddCspReportStorage(this IServiceCollection services)
    {
        services.AddScoped<CspReportStorageService>();
        return services;
    }
}
