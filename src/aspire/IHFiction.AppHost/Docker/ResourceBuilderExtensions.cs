using Aspire.Hosting.Docker;

namespace IHFiction.AppHost.Docker;

#pragma warning disable ASPIRECOMPUTE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPUBLISHERS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

/// <summary>
/// Provides extension methods for <see cref="IResourceBuilder{T}"/> to support pushing resources to a container registry.
/// </summary>
internal static class ResourceBuilderExtensions
{
    /// <summary>
    /// Configures a Docker Compose environment resource to push its images to a container registry during deployment.
    /// </summary>
    /// <param name="builder">The resource builder for the Docker Compose environment resource.</param>
    /// <param name="registry">The container registry to push the images to. If null, the default container registry of the resource is used.</param>
    /// <returns>The resource builder with the registry deployment annotation.</returns>
    internal static IResourceBuilder<DockerComposeEnvironmentResource> PushToRegistry(
        this IResourceBuilder<DockerComposeEnvironmentResource> builder,
        string? registry = null
    ) => DockerComposeDeploymentContext.AddRegistryDeployment(builder, registry);

    /// <summary>
    /// Configures a compute resource to be pushed to a container registry during deployment.
    /// </summary>
    /// <typeparam name="T">The type of the compute resource.</typeparam>
    /// <param name="builder">The resource builder for the compute resource.</param>
    /// <param name="registry">The container registry to push the image to.</param>
    /// <param name="tag">The tag to use for the image.</param>
    /// <returns>The resource builder with the registry deployment annotation.</returns>
    public static IResourceBuilder<T> PushToRegistry<T>(this IResourceBuilder<T> builder, string registry, string tag = "latest") where T : IComputeResource
    {
        // ? This need checking? No mode for deploy
        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode) return builder;

        // TODO: Non-Project resources would need to be pulled first before pushing. Skip them for now.
        if (builder.Resource is not ProjectResource) return builder;

        return builder.WithAnnotation(new DeployingCallbackAnnotation(RegistryDeploymentFactory.CreateDeploymentCallback(
            registry,
            builder.Resource.Name,
            tag)), ResourceAnnotationMutationBehavior.Replace);
    }
}
