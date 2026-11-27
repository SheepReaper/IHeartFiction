using System.Text;

namespace IHFiction.AppHost.Extensions;

internal static class WatchdogComposeExtensions
{
    public static IResourceBuilder<ContainerResource> AddWatchdog(
        this IDistributedApplicationBuilder builder, string name, string image, string dockerHost,
        string stackName, int intervalSeconds = 15, int failureThreshold = 3, int cooldownSeconds = 300)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(stackName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failureThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cooldownSeconds);

        using var source = typeof(WatchdogComposeExtensions).Assembly.GetManifestResourceStream("IHFiction.AppHost.watchdog.main.go")
            ?? throw new InvalidOperationException("Watchdog source was not embedded in the AppHost.");
        using var reader = new StreamReader(source);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
        var watchdog = builder.AddContainer(name, image);
        watchdog.PublishAsDockerComposeService((_, service) =>
        {
            service.Entrypoint = ["/bin/sh", "-c"];
            service.Command = [$"echo {encoded} | base64 -d > /tmp/watchdog.go && exec go run /tmp/watchdog.go"];
            service.Environment["DOCKER_HOST"] = dockerHost;
            service.Environment["WATCHDOG_TARGETS"] = string.Join(',', WatchdogAnnotationRegistry.GetAll().Select(target =>
                $"{stackName}_{target.ServiceName}:{target.Host}:{target.Port}:{target.RestartOnFailure}"));
            service.Environment["WATCHDOG_INTERVAL_SECONDS"] = intervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            service.Environment["WATCHDOG_FAILURE_THRESHOLD"] = failureThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            service.Environment["WATCHDOG_RECOVERY_COOLDOWN_SECONDS"] = cooldownSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            service.Environment["GOCACHE"] = "/root/.cache/go-build";
        });
        return watchdog;
    }
}
