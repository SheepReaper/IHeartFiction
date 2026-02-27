#pragma warning disable ASPIREPROBES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Globalization;
using System.Text;

using Aspire.Hosting.Docker.Resources.ComposeNodes;

using Humanizer;

namespace IHFiction.AppHost.Extensions;

internal static class DockerComposeExtensions
{
    public static IResourceBuilder<TResource> WithDockerHealthcheck<TResource>(
        this IResourceBuilder<TResource> builder,
        IResourceBuilder<ParameterResource>? portParameter = null,
        Action<DockerHealthcheckOptions>? configureOptions = null
    ) where TResource : IComputeResource, IResourceWithProbes =>
        builder.WithDockerHealthcheck(WellKnownTests.Wget, portParameter, configureOptions);

    public static IResourceBuilder<TResource> WithDockerHealthcheck<TResource>(
        this IResourceBuilder<TResource> builder,
        IEnumerable<string> test,
        Action<DockerHealthcheckOptions>? configureOptions = null
    ) where TResource : IComputeResource, IResourceWithProbes => builder.PublishAsDockerComposeService((_, node) =>
        node.SetHealthcheck(test, new DockerHealthcheckOptions(configureOptions)));

    public static IResourceBuilder<TResource> WithDockerHealthcheck<TResource>(
        this IResourceBuilder<TResource> builder,
        WellKnownTests type,
        IResourceBuilder<ParameterResource>? portParameter = null,
        Action<DockerHealthcheckOptions>? configureOptions = null
    ) where TResource : IComputeResource, IResourceWithProbes => builder.PublishAsDockerComposeService(async (sr, node) =>
    {
        if (!(builder.Resource.Annotations.OfType<EndpointProbeAnnotation>().FirstOrDefault(p => p.Type == ProbeType.Liveness) is { } probe))
            throw new DistributedApplicationException($"Resource '{builder.Resource.Name}' does not have a liveness probe annotation.");

        portParameter ??= builder.ApplicationBuilder.AddParameter(
            name: $"{builder.Resource.Name}_healthcheck_port".Pascalize(),
            valueGetter: () => probe.EndpointReference.TargetPort?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

        var scheme = probe.EndpointReference.Scheme;

        var port = await portParameter.Resource.GetValueAsync(CancellationToken.None);

        if (string.IsNullOrWhiteSpace(port))
            port = new ParameterResource($"{builder.Resource.Name}-healthcheck-port", (_) => string.Empty).AsEnvironmentPlaceholder(sr);

        var path = probe.Path;

        string[] test = type switch
        {
            WellKnownTests.NetCat => ["CMD", "nc", "-z", "localhost", port],
            WellKnownTests.Wget => ["CMD", "wget", "--spider", "-q", $"{scheme}://localhost:{port}{path}"],
            WellKnownTests.Curl => ["CMD", "curl", "-f", $"{scheme}://localhost:{port}{path}"],
            _ => throw new DistributedApplicationException($"Unsupported healthcheck test type: {type}")
        };

        var options = probe.ToDockerHealthcheckOptions();

        configureOptions?.Invoke(options);

        node.SetHealthcheck(test, options);
    });

    private static void SetHealthcheck(
        this Service serviceNode,
        IEnumerable<string> test,
        DockerHealthcheckOptions options
    )
    {
        var (intervalSeconds, timeoutSeconds, startPeriodSeconds, retries, _) = options;

        serviceNode.Healthcheck = new()
        {
            Test = [.. test],
            Interval = TimeSpan.FromSeconds(intervalSeconds ?? 30).ToDockerDuration(),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds ?? 30).ToDockerDuration(),
            StartPeriod = TimeSpan.FromSeconds(startPeriodSeconds ?? 0).ToDockerDuration(),
            // Note: Aspire.Hosting.Docker does not support StartInterval
            // StartInterval = TimeSpan.FromSeconds(startIntervalSeconds ?? 5).ToDockerDuration()
        };

        if (retries.HasValue)
            serviceNode.Healthcheck.Retries = retries.Value;
    }

    public static DockerHealthcheckOptions ToDockerHealthcheckOptions(this ProbeAnnotation probe) => new()
    {
        IntervalSeconds = probe.PeriodSeconds,
        TimeoutSeconds = probe.TimeoutSeconds,
        StartPeriodSeconds = probe.InitialDelaySeconds,
        Retries = probe.FailureThreshold,
        // StartIntervalSeconds = probe.InitialPeriodSeconds // This relatively new Docker engine feature is not supported Aspire Probes.
    };

    public static string ToDockerDuration(this TimeSpan ts)
    {
        if (ts.TotalSeconds < 1)
            return "0s";

        StringBuilder sb = new();

        if (ts.Days > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Days}d");

        if (ts.Hours > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Hours}h");

        if (ts.Minutes > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Minutes}m");

        if (ts.Seconds > 0)
            sb.Append(CultureInfo.InvariantCulture, $"{ts.Seconds}s");

        return sb.Length == 0 ? "0s" : sb.ToString();
    }
}
