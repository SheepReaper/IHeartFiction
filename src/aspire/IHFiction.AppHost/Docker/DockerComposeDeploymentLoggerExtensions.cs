using Microsoft.Extensions.Logging;

namespace IHFiction.AppHost.Docker;

internal static partial class DockerComposeDeploymentLoggerExtensions {
    [LoggerMessage(LogLevel.Information, "Pushing images to registry")]
    internal static partial void StartPushingImages(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "No resources found in the model")]
    internal static partial void EmptyModel(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Successfully pushed all images to {registry}")]
    internal static partial void FinishPushingImages(this ILogger logger, string registry);

    [LoggerMessage(LogLevel.Warning, "Not in publishing mode. Skipping pushing images.")]
    internal static partial void NotInPublishingMode(this ILogger logger);
}
