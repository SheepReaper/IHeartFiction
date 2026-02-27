namespace IHFiction.AppHost.Extensions;

internal sealed class DockerHealthcheckOptions
{
    public int? IntervalSeconds { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? StartPeriodSeconds { get; set; }
    public int? Retries { get; set; }
    public int? StartIntervalSeconds { get; set; }

    public void Deconstruct(
        out int? intervalSeconds,
        out int? timeoutSeconds,
        out int? startPeriodSeconds,
        out int? retries,
        out int? startIntervalSeconds)
    {
        intervalSeconds = IntervalSeconds;
        timeoutSeconds = TimeoutSeconds;
        startPeriodSeconds = StartPeriodSeconds;
        retries = Retries;
        startIntervalSeconds = StartIntervalSeconds;
    }

    public DockerHealthcheckOptions(Action<DockerHealthcheckOptions>? configure = null)
    {
        configure?.Invoke(this);
    }
}