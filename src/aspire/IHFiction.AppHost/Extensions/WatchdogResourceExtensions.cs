namespace IHFiction.AppHost.Extensions;

internal static class WatchdogResourceExtensions
{
    public static IResourceBuilder<TResource> WithWatchdogRegistration<TResource>(
        this IResourceBuilder<TResource> builder,
        string host,
        int port,
        bool restartOnFailure)
        where TResource : IComputeResource
    {
        var target = new WatchdogTargetAnnotation(builder.Resource.Name, builder.Resource.Name, host, port, restartOnFailure);
        WatchdogAnnotationRegistry.Register(builder.Resource.Name, target);

        return builder.PublishAsDockerComposeService((_, service) =>
            WatchdogAnnotationRegistry.Register(builder.Resource.Name, target with { ServiceName = service.Name }));
    }
}

internal static class WatchdogAnnotationRegistry
{
    private static readonly Dictionary<string, WatchdogTargetAnnotation> Targets = [with(StringComparer.Ordinal)];

    public static void Register(string name, WatchdogTargetAnnotation target) => Targets[name] = target;

    public static WatchdogTargetAnnotation[] GetAll() => [.. Targets.Values];
}
