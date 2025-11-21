using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;

using Aspire.Hosting.Pipelines;

namespace IHFiction.AppHost.Extensions;

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal static partial class ResourceBuilderExtensions
{
    private static readonly SemaphoreSlim PushSemaphore = new(1, 1);

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Pushing image to registry for resource '{resourceName}'")]

    public static partial void LogPushStart(this ILogger logger, string resourceName);

    public static IResourceBuilder<TResource> PushToRegistry<TResource>(
        this IResourceBuilder<TResource> builder
    ) where TResource : IComputeResource => builder.WithPipelineStepFactory(ctx => new()
    {
        Name = $"registry-push-{ctx.Resource.Name}",
        RequiredBySteps = [WellKnownPipelineSteps.Publish],
        DependsOnSteps = [WellKnownPipelineSteps.Build],
        Action = async stepCtx =>
        {
            await PushSemaphore.WaitAsync(stepCtx.CancellationToken).ConfigureAwait(false);

            try
            {
                stepCtx.Logger.LogPushStart(ctx.Resource.Name);

                if (!ctx.Resource.TryGetLastAnnotation<IProjectMetadata>(out var projectMetadata))
                {
                    await stepCtx.ReportingStep.FailAsync(
                        $"Resource '{ctx.Resource.Name}' does not have project metadata annotation.",
                        stepCtx.CancellationToken).ConfigureAwait(false);

                    return;
                }

                if (!ctx.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
                {
                    await stepCtx.ReportingStep.FailAsync(
                        $"Resource '{ctx.Resource.Name}' does not have container image annotation.",
                        stepCtx.CancellationToken).ConfigureAwait(false);

                    return;
                }

                StringBuilder args = new(string.Join(' ',
                    $"publish \"{projectMetadata.ProjectPath}\"",
                    "--configuration Release",
                    "--no-build",
                    "-t:PublishContainer",
                    $"-p:ContainerRepository={image.Image}",
                    $"-p:ContainerRegistry={image.Registry}"));

                using Process process = new()
                {
                    StartInfo = new()
                    {
                        Arguments = args.ToString(),
                        FileName = "dotnet"
                    }
                };

                process.Start();

                if (!process.HasExited)
                    await process.WaitForExitAsync(stepCtx.CancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0) await stepCtx.ReportingStep.FailAsync(
                    $"Failed to push {image.Image} to {image.Registry}",
                    stepCtx.CancellationToken).ConfigureAwait(false);

                else await stepCtx.ReportingStep.SucceedAsync(
                    $"Successfully pushed {image.Image} to {image.Registry}",
                    stepCtx.CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                PushSemaphore.Release();
            }
        }
    });

    public static IResourceBuilder<TResource> WithImageRegistry<TResource>(this IResourceBuilder<TResource> builder) where TResource : IComputeResource
    {
        var repositoryPrefix = builder.ApplicationBuilder.Configuration["RepositoryPrefix"]?.Trim('/');

        if (!string.IsNullOrEmpty(repositoryPrefix))
            repositoryPrefix += "/";

        return builder.WithAnnotation<ContainerImageAnnotation>(new()
        {
            Registry = builder.ApplicationBuilder.Configuration["Registry"],
            Image = $"{repositoryPrefix}{builder.Resource.Name}",
            Tag = "latest"
        }, ResourceAnnotationMutationBehavior.Replace);
    }

    public static string ToTaggedString(this ContainerImageAnnotation image) => $"{image.Registry}/{image.Image}:{image.Tag}";
    // public static string ToSha256String(this ContainerImageAnnotation image) => $"{image.Registry}/{image.Image}@sha256:{image.SHA256}";
}
