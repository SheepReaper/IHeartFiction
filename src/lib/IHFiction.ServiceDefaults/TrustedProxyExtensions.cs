using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class TrustedProxyExtensions
{
    public static IHostApplicationBuilder AddTrustedProxyForwarding(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        var configureTrust = !builder.Environment.IsDevelopment();
        builder.Services.AddOptions<ForwardedHeadersOptions>()
            .Configure(options =>
            {
                if (configureTrust) ConfigureTrustedProxyOptions(options, configuration);
            })
            .Validate(_ => !string.Equals(configuration["ForwardedHeaders_Enabled"], "true", StringComparison.OrdinalIgnoreCase),
                "Automatic forwarded headers conflict with explicit proxy trust. Unset ASPNETCORE_FORWARDEDHEADERS_ENABLED, DOTNET_FORWARDEDHEADERS_ENABLED, and ForwardedHeaders_Enabled.")
            .ValidateOnStart();
        return builder;
    }

    public static WebApplication UseTrustedProxyForwarding(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
            app.UseForwardedHeaders();
        return app;
    }

    public static void ConfigureTrustedProxyOptions(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        var cidrs = configuration["TrustedProxies"]?.Split(',', StringSplitOptions.TrimEntries);
        if (cidrs is null || cidrs.Length == 0)
            throw new InvalidOperationException("TrustedProxies must contain explicit proxy CIDRs.");

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var cidr in cidrs)
        {
            if (!System.Net.IPNetwork.TryParse(cidr, out var network) || network.PrefixLength == 0)
                throw new InvalidOperationException("TrustedProxies contains an invalid or unrestricted CIDR.");
            options.KnownIPNetworks.Add(network);
        }
    }
}
