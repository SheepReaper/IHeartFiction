namespace IHFiction.AppHost.Extensions;

/// <summary>Marks a Swarm service as an endpoint monitored by the production watchdog.</summary>
internal sealed record WatchdogTargetAnnotation(string ResourceName, string ServiceName, string Host, int Port, bool RestartOnFailure);

/// <summary>Opt-in watchdog integration for Aspire compute resources.</summary>
internal static class WatchdogExtensions
{
    /// <summary>Monitor this service from the production Swarm overlay.</summary>
    public static IResourceBuilder<TResource> WithWatchdog<TResource>(
        this IResourceBuilder<TResource> builder,
        string? host = null,
        int port = 8080,
        bool restartOnFailure = false)
        where TResource : IComputeResource
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);

        return builder.WithWatchdogRegistration(host ?? builder.Resource.Name, port, restartOnFailure);
    }
}
