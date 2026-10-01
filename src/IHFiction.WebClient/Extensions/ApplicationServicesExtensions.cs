using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;

namespace IHFiction.WebClient.Extensions;

internal static class ApplicationServicesExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        // UI & Blazor services
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(options =>
            {
                // HACK: Increase the maximum message size to handle large pastes in the content editor
                // This should be revisited to use chunking from the client side
                options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
            });

        // Browser storage & client state
        builder.Services.AddScoped<ProtectedLocalStorage>();
        builder.Services.AddScoped<BrowserProtectedStorageService>();
        builder.Services.AddScoped<DeviceIdentityService>();
        builder.Services.AddScoped<ReadTrackingService>();
        builder.Services.AddScoped<ReaderProgressService>();
        builder.Services.AddScoped<ThemeService>();
        builder.Services.AddScoped<ViewPreferencesService>();
        builder.Services.AddScoped<StoryEditorService>();
        builder.Services.AddScoped<MetadataUrlService>();
        builder.Services.AddScoped<LoaderService>();
        builder.Services.AddScoped<TagLandingRequestContext>();

        // API HTTP Client & handlers
        builder.Services.AddTransient<AuthenticationHandler>();
        builder.Services.AddHttpClient<FictionApiClient>(client =>
            client.BaseAddress = new("https+http://fiction"))
                .AddHttpMessageHandler<AuthenticationHandler>();

        builder.Services.AddTransient<IFictionApiClient>(services => services.GetRequiredService<FictionApiClient>());

        // Application facade services
        builder.Services.AddTransient<AccountService>();
        builder.Services.AddTransient<AdminService>();
        builder.Services.AddTransient<AuthorService>();
        builder.Services.AddTransient<BookService>();
        builder.Services.AddTransient<ChapterService>();
        builder.Services.AddTransient<NotificationService>();
        builder.Services.AddTransient<StoryService>();
        builder.Services.AddTransient<TagService>();
        builder.Services.AddTransient<WorkService>();
        builder.Services.AddTransient<IPublishedWorkReader, WorkService>();
        builder.Services.AddTransient<MarkdownReaderService>();

        return builder;
    }
}
