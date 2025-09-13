using Microsoft.Extensions.Options;

using IHFiction.FictionApi.Authors;

namespace IHFiction.FictionApi.Extensions;

internal sealed class KeycloakAdminClientOptions
{
    public required Uri Host { get; set; }
    public required string AuthClientId { get; set; }
    public required string AuthClientSecret { get; set; }
    public string? Realm { get; set; }
    public TimeSpan TokenRefreshThreshold { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
}

internal static class KeycloakRealmAdminClientExtensions
{
    private const string DefaultRealm = "master";

    // Make for generic
    public static IServiceCollection AddKeycloakRealmAdminClient<TService>(
        this IServiceCollection services,
        string keycloakHostUri,
        string clientId,
        string realm = DefaultRealm,
        Action<KeycloakAdminClientOptions>? configureOptions = null
    ) where TService : class => services.AddKeycloakRealmAdminClient<TService>(new Uri(keycloakHostUri), clientId, realm, configureOptions);

    // Make for generic
    public static IServiceCollection AddKeycloakRealmAdminClient<TService>(
        this IServiceCollection services,
        Uri keycloakHostUri,
        string clientId,
        string realm = DefaultRealm,
        Action<KeycloakAdminClientOptions>? configureOptions = null
    ) where TService : class => services.AddKeycloakRealmAdminClient<TService>((options) =>
    {
        options.Host = keycloakHostUri;
        options.AuthClientId = clientId;
        options.Realm = realm;
        configureOptions?.Invoke(options);
    });

    public static IServiceCollection AddKeycloakRealmAdminClient<TService>(
        this IServiceCollection services,
        Action<KeycloakAdminClientOptions> configureOptions
    ) where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<KeycloakAdminClientOptions>()
            .Configure<IConfiguration>((options, config) => configureOptions.Invoke(options))
            .BindConfiguration(nameof(KeycloakAdminClientOptions))
            .PostConfigure((options) => options.Realm ??= DefaultRealm);

        services.AddHttpClient<TService>((serviceProvider, httpClient) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<KeycloakAdminClientOptions>>().Value;

            httpClient.BaseAddress = config.Host;
            httpClient.Timeout = config.Timeout;
        });

        return services;
    }

    public static IServiceCollection AddKeycloakRealmAdminClient(
        this IServiceCollection services,
        string serviceName,
        string clientId,
        string realm
    ) => AddKeycloakRealmAdminClient<KeycloakAdminService>(services, GetServerUri(serviceName), clientId, realm);

    private static string GetServerUri(string serviceName) {
        return $"https+http://{serviceName}";
    }
}