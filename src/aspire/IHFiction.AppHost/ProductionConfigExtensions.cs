using Aspire.Hosting.Docker;

namespace IHFiction.AppHost;

internal static class ProductionConfigExtensions
{
    const string AdminNetwork = "t3_proxy";
    const string ContainerNetwork = "containers";
    const string FrontEndNetwork = "ihf_proxy";
    const string DataPath = "/mnt/swarm/data/ihfiction";
    const string SecretsPath = "/mnt/swarm/config/ihfiction/secrets";

    public static IResourceBuilder<DockerComposeEnvironmentResource> ConfigureSwarmCompose(this IDistributedApplicationBuilder builder) => builder
        .AddDockerComposeEnvironment("internal")
        .WithDashboard(dash => dash.PublishAsDockerComposeService((_, service) =>
        {
            service.Networks.Add(AdminNetwork);

            service.AddVolume(new()
            {
                Name = "dashboard-data",
                Type = "bind",
                Source = $"{DataPath}/dashboard",
                Target = "/home/app/.aspnet/DataProtection-Keys"
            });
        }))
        .ConfigureComposeFile(file =>
        {
            file.AddNetwork(new()
            {
                Name = FrontEndNetwork,
                External = true
            })
            .AddNetwork(new()
            {
                Name = AdminNetwork,
                External = true
            })
            .AddNetwork(new()
            {
                Name = ContainerNetwork
            });

            file.Secrets.Add("keycloak-conf", new() { File = $"{SecretsPath}/keycloak.conf" });
            file.Secrets.Add("mongodb-root-pass", new() { File = $"{SecretsPath}/mongodb-root-pass.secret" });
            file.Secrets.Add("postgres-pass", new() { File = $"{SecretsPath}/postgres-pass.secret" });

            file.Secrets.Add("ConnectionStrings__fiction-db", new() { File = $"{SecretsPath}/conn-fiction-db.secret" });
            file.Secrets.Add("ConnectionStrings__stories-db", new() { File = $"{SecretsPath}/conn-stories-db.secret" });

            file.Secrets.Add("Authentication__Schemes__Keycloak__ClientSecret", new() { File = $"{SecretsPath}/keycloak-frontend-client.secret" });
            file.Secrets.Add("KeycloakAdminClientOptions__AuthClientSecret", new() { File = $"{SecretsPath}/keycloak-admin-client.secret" });

            // Cleanup noise for swarm spec
            foreach (var (_, service) in file.Services)
            {
                service.DependsOn.Clear(); // BUG: long-format of depends on is incompatible with swarm parser, and it's ignored anyways when short-form
                service.Expose = []; // Expose is ignored in swarm
                service.Restart = null; // Restart policy is ignored in swarm for containers
            }
        })
        .WithProperties(props =>
        {
            props.DefaultNetworkName = ContainerNetwork;
            props.DefaultContainerRegistry = builder.Configuration["Registry"];
        });

    public static IResourceBuilder<PostgresServerResource> ConfigureForSwarm(this IResourceBuilder<PostgresServerResource> builder) => builder.PublishAsDockerComposeService((_, service) =>
    {
        // Using secrets for postgres password
        service.Environment.Remove("POSTGRES_PASSWORD");
        service.Environment["POSTGRES_PASSWORD_FILE"] = "/run/secrets/postgres-pass";

        service.Secrets.Add(new() { Source = "postgres-pass" });

        service.Healthcheck = new()
        {
            Test = ["CMD-SHELL", "pg_isready -U postgres || exit 1"],
            Interval = "10s", // Bug: this should be optional in swarm
            Timeout = "5s", // Bug: this should be optional in swarm
            Retries = 12,
            StartPeriod = "0s" // Bug: this should be optional in swarm
        };

        service.AddVolume(new()
        {
            Name = "postgres-data",
            Type = "bind",
            Source = $"{DataPath}/postgres",
            Target = "/var/lib/postgresql/data",
            ReadOnly = false
        });
    });

    public static IResourceBuilder<MongoDBServerResource> ConfigureForSwarm(this IResourceBuilder<MongoDBServerResource> builder) => builder.PublishAsDockerComposeService((_, service) =>
    {
        // Using secrets for root password
        service.Environment.Remove("MONGO_INITDB_ROOT_PASSWORD");
        service.Environment["MONGO_INITDB_ROOT_PASSWORD_FILE"] = "/run/secrets/mongodb-root-pass";

        service.Secrets.Add(new() { Source = "mongodb-root-pass" });

        service.AddVolume(new()
        {
            Name = "mongodb-data",
            Type = "bind",
            Source = $"{DataPath}/mongo",
            Target = "/data/db"
        });
    });

    public static IResourceBuilder<KeycloakResource> ConfigureForSwarm(this IResourceBuilder<KeycloakResource> builder) => builder
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .PublishAsDockerComposeService((_, service) =>
        {
            // Using conf file
            service.Environment.Remove("KC_BOOTSTRAP_ADMIN_PASSWORD");

            service.Networks.Add(FrontEndNetwork);
            service.Secrets.Add(new() { Source = "keycloak-conf", Target = "/opt/keycloak/conf/keycloak.conf", Mode = 0444 });

            service.Command = ["start"];
            service.Deploy ??= new();
            service.Deploy.Replicas = 1;
        });

    public static IResourceBuilder<ProjectResource> ConfigureMigrationsForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .PublishAsDockerComposeService((res, service) =>
        {
            // Using secrets for connection strings
            service.Environment.Remove("ConnectionStrings__fiction-db");
            service.Environment.Remove("ConnectionStrings__stories-db");

            if (builder.ApplicationBuilder.Configuration["SecretsPath"] is string secretsPath)
                service.Environment["SecretsPath"] = secretsPath;

            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });

            service.Deploy ??= new();
            service.Deploy.Mode = "replicated-job";
            service.Deploy.Replicas = 1;

            if (builder.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
                service.Image = image.ToTaggedString();
        });

    public static IResourceBuilder<ProjectResource> ConfigureFictionApiForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .PublishAsDockerComposeService((res, service) =>
        {
            // Using secrets for connection strings
            service.Environment.Remove("ConnectionStrings__fiction-db");
            service.Environment.Remove("ConnectionStrings__stories-db");

            var config = builder.ApplicationBuilder.Configuration;

            if (config["Api:AllowedHosts"] is string allowedHosts)
                service.Environment["AllowedHosts"] = allowedHosts;

            if (config["Api:AllowedOrigins"] is string allowedOrigins)
                service.Environment["AllowedOrigins"] = allowedOrigins;

            if (config["OidcAuthority"] is string authority)
                service.Environment["OidcAuthority"] = authority;

            if (config["SecretsPath"] is string secretsPath)
                service.Environment["SecretsPath"] = secretsPath;

            if (config["TrustedProxies"] is string trustedProxies)
                service.Environment["TrustedProxies"] = trustedProxies;

            service.Deploy ??= new();

            // BUG: This should be set automatically via ReplicaAnnotation
            service.Deploy.Replicas = res.GetReplicaCount();

            service.Networks.Add(FrontEndNetwork);
            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });
            service.Secrets.Add(new() { Source = "KeycloakAdminClientOptions__AuthClientSecret" });

            if (builder.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
                service.Image = image.ToTaggedString();
        });

    public static IResourceBuilder<ProjectResource> ConfigureWebClientForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .PublishAsDockerComposeService((res, service) =>
        {
            var config = builder.ApplicationBuilder.Configuration;

            if (config["WebClient:AllowedHosts"] is string allowedHosts)
                service.Environment["AllowedHosts"] = allowedHosts;

            if (config["WebClient:ApiBaseAddress"] is string apiBaseAddress)
                service.Environment["ApiBaseAddress"] = apiBaseAddress;

            if (config["OidcAuthority"] is string authority)
                service.Environment["OidcAuthority"] = authority;

            if (config["SecretsPath"] is string secretsPath)
                service.Environment["SecretsPath"] = secretsPath;

            if (config["TrustedProxies"] is string trustedProxies)
                service.Environment["TrustedProxies"] = trustedProxies;

            service.Deploy ??= new();

            // BUG: This should be set automatically via ReplicaAnnotation
            service.Deploy.Replicas = res.GetReplicaCount();

            service.Networks.Add(FrontEndNetwork);
            service.Secrets.Add(new() { Source = "Authentication__Schemes__Keycloak__ClientSecret" });

            if (builder.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
                service.Image = image.ToTaggedString();
        });
}