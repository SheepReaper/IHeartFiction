using System.Globalization;
using System.Text;

using Aspire.Hosting.Docker.Resources.ComposeNodes;

namespace IHFiction.AppHost.Extensions;

internal static class DockerSwarmExtensions
{
    internal sealed record TraefikServiceDef(int Port = 8080, bool StickyCookie = false);
    internal sealed record TraefikRouterDef(string Entrypoint, string Host, string[]? LimitPrefixes = null);

    public static Service WithTraefikLabels(this Service service, string network, TraefikServiceDef serviceDef, params IEnumerable<TraefikRouterDef> routerDefs)
    {
        service.Deploy ??= new();
        service.Deploy.Labels ??= [];

        var key = $"ihfiction-{service.Name}";
        var labels = service.Deploy.Labels;

        labels["traefik.enable"] = "true";
        labels["traefik.swarm.network"] = network;

        labels[$"traefik.http.services.{key}.loadbalancer.server.port"] = serviceDef.Port.ToString(CultureInfo.InvariantCulture);

        if (serviceDef.StickyCookie)
            labels[$"traefik.http.services.{key}.loadbalancer.sticky.cookie"] = "{}";

        foreach (var (i, def) in routerDefs.Index())
        {
            var suffix = i == 0 ? "" : $"-{i}";

            StringBuilder ruleBuilder = new($"Host(`{def.Host}`)");

            var prefixes = string.Join(" || ", def.LimitPrefixes?.Select(p => $"PathPrefix(`{p}`)") ?? []);

            if (!string.IsNullOrWhiteSpace(prefixes))
                ruleBuilder.Append(CultureInfo.InvariantCulture, $" && ({prefixes})");

            labels[$"traefik.http.routers.{key}{suffix}.entrypoints"] = $"{def.Entrypoint}";
            labels[$"traefik.http.routers.{key}{suffix}.service"] = $"{key}";
            labels[$"traefik.http.routers.{key}{suffix}.rule"] = ruleBuilder.ToString();
        }

        return service;
    }

    public static Service AddGracefulUpdate(this Service service)
    {
        service.Deploy ??= new();
        service.Deploy.UpdateConfig = new()
        {
            // Parallelism = "1", // BUG: this should actually be an int [Fixed](https://github.com/dotnet/aspire/pull/11706)
            Delay = "10s",
            Monitor = "60s",
            Order = "start-first",
            // FailOnError = true // BUG: this should actually be a string [Fixed](https://github.com/dotnet/aspire/pull/11706)
        };

        return service;
    }
}