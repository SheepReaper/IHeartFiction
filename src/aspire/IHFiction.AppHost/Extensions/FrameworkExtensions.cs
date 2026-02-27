namespace IHFiction.AppHost.Extensions;

internal static class FrameworkExtensions
{
    public static async Task<string?> ToRegistryStringAsync(this ContainerRegistryReferenceAnnotation reference, CancellationToken cancellationToken = default)
    {
        var registry = await reference.Registry.Endpoint.GetValueAsync(cancellationToken).ConfigureAwait(false);
        var repository = reference.Registry.Repository is { } repo ? await repo.GetValueAsync(cancellationToken).ConfigureAwait(false) : null;

        return repository is null ? registry : $"{registry}/{repository}";
    }
}