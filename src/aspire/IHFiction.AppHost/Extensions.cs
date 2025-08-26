using Aspire.Hosting.Postgres;

namespace IHFiction.AppHost;

internal static class Extensions
{
    public static PgAdminContainerResource WaitForServer(this PgAdminContainerResource resource, PostgresServerResource postgresServerResource)
    {
        resource.Annotations.Add(new WaitAnnotation(postgresServerResource, WaitType.WaitUntilHealthy));

        return resource;
    }
    public static PgAdminContainerResource WaitForServer(this PgAdminContainerResource resource, IResourceBuilder<PostgresServerResource> postgresServerResourceBuilder) =>
        resource.WaitForServer(postgresServerResourceBuilder.Resource);

    public static PgAdminContainerResource GetPgAdminContainer(this IResourceBuilder<PostgresServerResource> resource)
    {
        var pgAdmin = resource.ApplicationBuilder.Resources.OfType<PgAdminContainerResource>().FirstOrDefault();

        return pgAdmin is null
            ? throw new InvalidOperationException("PgAdmin container not found. Please add PgAdmin container to the resource builder.")
            : pgAdmin;
    }
}