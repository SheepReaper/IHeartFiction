using Microsoft.Extensions.Logging;

using Aspire.Hosting.Docker;
using Aspire.Hosting.Publishing;

namespace IHFiction.AppHost.Docker;

#pragma warning disable ASPIREPUBLISHERS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
/// <summary>
/// Provides context for deploying Docker Compose projects to a container registry.
/// This class is responsible for pushing the container images of the projects in the application model to a specified registry.
/// </summary>
/// <param name="executionContext">The execution context of the distributed application.</param>
/// <param name="logger">The logger to use for logging.</param>
/// <param name="activityReporter">The reporter for publishing activities.</param>
/// <param name="registry">The container registry to push the images to.</param>
/// <param name="tag">The tag to use for the images.</param>
/// <param name="cancellationToken">A cancellation token to observe while waiting for the operation to complete.</param>
internal sealed class DockerComposeDeploymentContext(
    DistributedApplicationExecutionContext executionContext,
    ILogger logger,
    IPublishingActivityReporter activityReporter,
    string registry,
    string tag,
    CancellationToken cancellationToken = default)
{
    /// <summary>
    /// Adds a registry deployment annotation to a Docker Compose environment resource.
    /// This annotation triggers the push of the container images to the specified registry during deployment.
    /// </summary>
    /// <param name="builder">The resource builder for the Docker Compose environment resource.</param>
    /// <param name="registry">The container registry to push the images to. If null, the default container registry of the resource is used.</param>
    /// <param name="tag">The tag to use for the images.</param>
    /// <returns>The resource builder with the registry deployment annotation.</returns>
    public static IResourceBuilder<DockerComposeEnvironmentResource> AddRegistryDeployment(
        IResourceBuilder<DockerComposeEnvironmentResource> builder,
        string? registry = null,
        string tag = "latest")
    {
        registry ??= builder.Resource.DefaultContainerRegistry;

        ArgumentException.ThrowIfNullOrWhiteSpace(registry);

        return builder.WithAnnotation(new DeployingCallbackAnnotation(context =>
        {
            var pushingContext = new DockerComposeDeploymentContext(
                context.ExecutionContext,
                context.Logger,
                context.ActivityReporter,
                registry,
                tag,
                context.CancellationToken
            );

            return pushingContext.PushImagesAsync(context.Model, context);
        }));
    }

    /// <summary>
    /// Pushes the container images of the projects in the application model to the specified registry.
    /// </summary>
    /// <param name="model">The distributed application model.</param>
    /// <param name="context">The deployment context.</param>
    internal async Task PushImagesAsync(DistributedApplicationModel model, DeployingContext context)
    {
        if (!executionContext.IsPublishMode)
        {
            logger.NotInPublishingMode();
            return;
        }

        if (model.Resources.Count == 0)
        {
            logger.EmptyModel();
            return;
        }

        await PushImagesInternalAsync(model, context).ConfigureAwait(false);

        logger.FinishPushingImages(registry);
    }

    /// <summary>
    /// Pushes the container images of the projects in the application model to the specified registry.
    /// This method is for internal use and is called by <see cref="PushImagesAsync"/>.
    /// </summary>
    /// <param name="model">The distributed application model.</param>
    /// <param name="context">The deployment context.</param>
    private async Task PushImagesInternalAsync(DistributedApplicationModel model, DeployingContext context)
    {
        var step = await activityReporter.CreateStepAsync(
            $"Deploying container images to registry: {registry}.",
            cancellationToken).ConfigureAwait(false);

        await using (step.ConfigureAwait(false))
        {
            var resources = model.Resources.OfType<ProjectResource>();

            foreach (var resource in resources)
            {
                var name = resource.Name;

                // Skip resources that have their own deployment logic
                if (resource.HasAnnotationOfType<DeployingCallbackAnnotation>())
                {
                    var skippedTask = await step.CreateTaskAsync(
                        $"SKIPPED: {name}.",
                        cancellationToken).ConfigureAwait(false);

                    await using (skippedTask.ConfigureAwait(false))
                    {
                        await skippedTask.SucceedAsync(
                            $"Skipped image for {name} because it ran its own deployment logic.",
                            cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                var deployment = RegistryDeploymentFactory.CreateDeploymentCallback(registry, name, tag, step);

                await deployment.Invoke(context).ConfigureAwait(false);
            }

            await step.SucceedAsync(
                $"Successfully pushed all images to {registry}.",
                cancellationToken).ConfigureAwait(false);
        }
    }
}