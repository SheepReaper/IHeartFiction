using System.Diagnostics;

using Aspire.Hosting.Publishing;

namespace IHFiction.AppHost.Docker;

#pragma warning disable ASPIREPUBLISHERS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

/// <summary>
/// A factory for creating registry deployment callbacks.
/// </summary>
internal static class RegistryDeploymentFactory
{
    /// <summary>
    /// Creates a deployment callback that tags and pushes a Docker image to a registry.
    /// </summary>
    /// <param name="registry">The registry to push the image to.</param>
    /// <param name="imageName">The name of the image.</param>
    /// <param name="imageTag">The tag of the image.</param>
    /// <param name="parentStep">An optional parent publishing step.</param>
    /// <returns>A function that performs the deployment.</returns>
    public static Func<DeployingContext, Task> CreateDeploymentCallback(string registry, string imageName, string imageTag, IPublishingStep? parentStep = null) => async (ctx) =>
    {
        var finishStep = parentStep is null;

        parentStep ??= await ctx.ActivityReporter.CreateStepAsync(
            $"Deploying built image for {imageName} to registry {registry}.",
            ctx.CancellationToken).ConfigureAwait(false);

        var localTag = $"{imageName}:{imageTag}";
        var remoteTag = $"{registry.Trim().TrimEnd('/')}/{localTag}";

        var tagTask = await parentStep.CreateTaskAsync(
            $"Tagging image for {imageName} from {localTag} to {remoteTag}.",
            ctx.CancellationToken).ConfigureAwait(false);

        await using (tagTask.ConfigureAwait(false))
        {
            await RunAsync("docker", $"tag {localTag} {remoteTag}", ctx.CancellationToken).ConfigureAwait(false);

            await tagTask.SucceedAsync(
                $"Successfully tagged image for {imageName} from {localTag} to {remoteTag}.",
                ctx.CancellationToken).ConfigureAwait(false);
        }

        var pushTask = await parentStep.CreateTaskAsync(
            $"Pushing image for {imageName} to {remoteTag}.",
            ctx.CancellationToken).ConfigureAwait(false);

        await using (pushTask.ConfigureAwait(false))
        {
            await RunAsync("docker", $"push {remoteTag}", ctx.CancellationToken).ConfigureAwait(false);

            await pushTask.SucceedAsync(
                $"Successfully pushed image for {imageName} to {remoteTag}.",
                ctx.CancellationToken).ConfigureAwait(false);
        }

        if (finishStep) await parentStep.SucceedAsync(
            $"Successfully deployed image for {imageName} to registry {registry}.",
            ctx.CancellationToken).ConfigureAwait(false);
    };

    /// <summary>
    /// Runs a process asynchronously.
    /// </summary>
    /// <param name="file">The file to run.</param>
    /// <param name="args">The arguments to pass to the file.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the process to exit.</param>
    /// <exception cref="InvalidOperationException">Thrown when the process fails to start or exits with a non-zero exit code.</exception>
    private static async Task RunAsync(string file, string args, CancellationToken ct)
    {
        ProcessStartInfo psi = new(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");

        await p.WaitForExitAsync(ct).ConfigureAwait(false);

        // TODO: Naive implementation. Should parse error code to give better idea of what failed.
        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"Process '{file} {args}' failed with exit code {p.ExitCode}. If target registry requires authentication, make sure docker is logged in.");

    }
}