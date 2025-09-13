using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

using Aspire.Hosting.Publishing;

namespace IHFiction.AppHost.Extensions;

#pragma warning disable ASPIRECOMPUTE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPUBLISHERS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal static class ResourceBuilderExtensions
{
    private static IPublishingStep? s_deployStep;
    private static ConcurrentBag<string>? s_pendingPushes;
    private static volatile bool s_stepFailed;
    private static readonly SemaphoreSlim DeployStepLock = new(1, 1);

    public static IResourceBuilder<TResource> PushToRegistry<TResource>(
        this IResourceBuilder<TResource> builder,
        bool build = false
    ) where TResource : IComputeResource
    {
        if (!builder.Resource.TryGetLastAnnotation<IProjectMetadata>(out var projectMetadata))
            throw new InvalidOperationException($"Resource '{builder.Resource.Name}' does not have project metadata annotation.");

        if (!builder.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
            throw new InvalidOperationException($"Resource '{builder.Resource.Name}' does not have container image annotation.");

        StringBuilder args = new(string.Join(' ',
            $"publish \"{projectMetadata.ProjectPath}\"",
            "--configuration Release",
            "-t:PublishContainer",
            $"-p:ContainerRepository={image.Image}",
            $"-p:ContainerRegistry={image.Registry}"));

        if (!build) args.Append(" --no-build");

        return builder.WithAnnotation<DeployingCallbackAnnotation>(new(async ctx =>
        {
            if (s_stepFailed) return;

            if (s_deployStep is null)
            {
                await DeployStepLock.WaitAsync(ctx.CancellationToken).ConfigureAwait(false);

                try
                {
                    if (s_deployStep is null)
                    {
                        s_deployStep = await ctx.ActivityReporter.CreateStepAsync(
                            $"Pushing container images to registry",
                            ctx.CancellationToken).ConfigureAwait(false);

                        s_pendingPushes = [.. ctx.Model.GetProjectResources()
                            .OfType<IComputeResource>()
                            .Where(r => r.HasAnnotationOfType<DeployingCallbackAnnotation>())
                            .Select(r => r.Name)];
                    }
                }
                finally
                {
                    DeployStepLock.Release();
                }
            }

            var task = await s_deployStep.CreateTaskAsync(
                $"Pushing {image.Image} to {image.Registry}",
                ctx.CancellationToken).ConfigureAwait(false);

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
                await process.WaitForExitAsync(ctx.CancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                s_stepFailed = true;

                await task.FailAsync(
                    $"Failed to push {image.Image} to {image.Registry}",
                    ctx.CancellationToken).ConfigureAwait(false);

                await s_deployStep.FailAsync(
                    $"Failed to push all images to registry",
                    ctx.CancellationToken).ConfigureAwait(false);
            }

            s_pendingPushes?.TryTake(out _);

            await task.SucceedAsync(
                $"Successfully pushed {image.Image} to {image.Registry}",
                ctx.CancellationToken).ConfigureAwait(false);

            if (s_pendingPushes is not { Count: > 0 } && !s_stepFailed)
                await s_deployStep.SucceedAsync(
                    $"Successfully pushed all images to registry",
                    ctx.CancellationToken).ConfigureAwait(false);
        }));
    }

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
    public static string ToSha256String(this ContainerImageAnnotation image) => $"{image.Registry}/{image.Image}@sha256:{image.SHA256}";
}
