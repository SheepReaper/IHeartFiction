using IHFiction.FictionApi.AgentAuth;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Markdown;
using IHFiction.SharedKernel.Notifications;

namespace IHFiction.FictionApi.Extensions;

internal static class ApplicationServicesExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        // Options
        builder.Services.Configure<MarkdownOptions>(builder.Configuration.GetSection(MarkdownOptions.SectionName));
        builder.Services.Configure<WebPushOptions>(builder.Configuration.GetSection("WebPush"));

        // Application services
        builder.Services.AddSingleton<KeycloakAdminService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<AgentTokenService>();
        builder.Services.AddScoped<AgentAssertionValidator>();
        builder.Services.AddScoped<AgentRegistrationService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<AuthorizationService>();
        builder.Services.AddScoped<EntityLoaderService>();

        builder.Services.AddTransient<LinkService>();

        // Configure pagination options
        builder.Services.AddPagination();

        // Automatically register all use case classes
        builder.Services.AddUseCases();

        // Register endpoints
        builder.Services.AddEndpoints();

        return builder;
    }
}
