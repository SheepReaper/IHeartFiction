#pragma warning disable ASPIREPIPELINES003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Aspire.Hosting.Docker;
using Aspire.Hosting.Publishing;

using static IHFiction.AppHost.Extensions.DockerSwarmExtensions;

namespace IHFiction.AppHost.Extensions;

internal static class ProductionConfigExtensions
{
    public static IResourceBuilder<TResource> WithCommonOptions<TResource>(
        this IResourceBuilder<TResource> builder
    ) where TResource : IComputeResource => builder
        .WithRemoteImageTag("latest")
        .WithContainerBuildOptions(context =>
        {
            context.ImageFormat = ContainerImageFormat.Oci;
            context.TargetPlatform = ContainerTargetPlatform.AllLinux;
            // context.TargetPlatform = ContainerTargetPlatform.LinuxAmd64;
        });

    const string AdminNetwork = "t3_proxy";
    const string ContainerNetwork = "containers";
    const string FrontEndNetwork = "ihf_proxy";
    const string DataPath = "/mnt/swarm/data/ihfiction";
    const string SecretsPath = "/mnt/swarm/config/ihfiction/secrets";

    public static IResourceBuilder<DockerComposeEnvironmentResource> ConfigureSwarmCompose(this IDistributedApplicationBuilder builder) => builder
        .AddDockerComposeEnvironment("internal")
        .WithDashboard(dash => dash
            .WithForwardedHeaders()
            .PublishAsDockerComposeService((_, service) =>
            {
                service.Networks.Add(AdminNetwork);

                service.AddVolume(new()
                {
                    Name = "dashboard-data",
                    Type = "bind",
                    Source = $"{DataPath}/dashboard",
                    Target = "/home/app/.aspnet/DataProtection-Keys"
                });

                service.Environment["ASPIRE_DASHBOARD_FILE_CONFIG_DIRECTORY"] = "/run/secrets";
                service.Environment["ASPIRE_DASHBOARD_FORWARDEDHEADERS_ENABLED"] = "true";
                service.Environment["DASHBOARD__OTLP__AUTHMODE"] = "ApiKey";

                service.Secrets.Add(new() { Source = "Dashboard__Otlp__PrimaryApiKey" });

                if (builder.Configuration["AdminEntrypoint"] is string adminEntryPoint
                    && builder.Configuration["DashboardDomain"] is string dashboardDomain)
                    service.WithTraefikLabels(
                        AdminNetwork,
                        new(18888),
                        new TraefikRouterDef(adminEntryPoint, dashboardDomain));
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
                Name = ContainerNetwork,
                Internal = true
            });

            file.Secrets.Add("keycloak-conf", new() { File = $"{SecretsPath}/keycloak.conf" });
            file.Secrets.Add("mongodb-root-pass", new() { File = $"{SecretsPath}/mongodb-root-pass.secret" });
            file.Secrets.Add("postgres-pass", new() { File = $"{SecretsPath}/postgres-pass.secret" });

            file.Secrets.Add("ConnectionStrings__fiction-db", new() { File = $"{SecretsPath}/conn-fiction-db.secret" });
            file.Secrets.Add("ConnectionStrings__stories-db", new() { File = $"{SecretsPath}/conn-stories-db.secret" });

            file.Secrets.Add("Authentication__Schemes__Keycloak__ClientSecret", new() { File = $"{SecretsPath}/keycloak-frontend-client.secret" });
            file.Secrets.Add("KeycloakAdminClientOptions__AuthClientSecret", new() { File = $"{SecretsPath}/keycloak-admin-client.secret" });

            file.Secrets.Add("Dashboard__Otlp__PrimaryApiKey", new() { File = $"{SecretsPath}/otlp-api-key.secret" });

            // Cleanup noise for swarm spec
            foreach (var (_, service) in file.Services)
            {
                service.DependsOn.Clear(); // BUG: long-format of depends on is incompatible with swarm parser, and it's ignored anyways when short-form
                service.Expose = []; // Expose is ignored in swarm
                service.Restart = null; // Container restart policy is ignored in swarm
            }
        })
        .WithProperties(props =>
        {
            props.DefaultNetworkName = ContainerNetwork;
        })
        .ConfigureEnvFile(file =>
        {
            file.Remove("FICTION_IMAGE");
            file.Remove("FICTION_PORT");
            file.Remove("KEYCLOAK_PASSWORD");
            file.Remove("MIGRATIONS_IMAGE");
            file.Remove("MONGO_PASSWORD");
            file.Remove("POSTGRES_PASSWORD");
            file.Remove("WEB_IMAGE");
            file.Remove("WEB_PORT");
        });

    public static IResourceBuilder<PostgresServerResource> ConfigureForSwarm(this IResourceBuilder<PostgresServerResource> builder) => builder
        .WithDockerHealthcheck(["CMD", "pg_isready", "-U", "postgres"])
        .PublishAsDockerComposeService((_, service) =>
        {
            // Using secrets for postgres password
            service.Environment.Remove("POSTGRES_PASSWORD");
            service.Environment["POSTGRES_PASSWORD_FILE"] = "/run/secrets/postgres-pass";

            service.Secrets.Add(new() { Source = "postgres-pass" });

            service.AddVolume(new()
            {
                Name = "postgres-data",
                Type = "bind",
                Source = $"{DataPath}/postgres",
                Target = "/var/lib/postgresql/data",
                ReadOnly = false
            });
        });

    public static IResourceBuilder<MongoDBServerResource> ConfigureForSwarm(this IResourceBuilder<MongoDBServerResource> builder) => builder
        .WithDockerHealthcheck(["CMD", "mongosh", "--eval", "db.adminCommand('ping')"])
        .PublishAsDockerComposeService((_, service) =>
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
        .WithDockerHealthcheck(["CMD-SHELL", "{ printf 'HEAD /health/ready HTTP/1.0\r\n\r\n' >&0; grep 'HTTP/1.0 200'; } 0<>/dev/tcp/localhost/9000"])
        .PublishAsDockerComposeService((_, service) =>
        {
            // Using conf file
            service.Environment.Remove("KC_BOOTSTRAP_ADMIN_PASSWORD");

            service.Environment["JAVA_OPTS_APPEND"] = "-Djgroups.bind.address=match-interface:eth2";

            service.Networks.Add(FrontEndNetwork);
            service.Secrets.Add(new() { Source = "keycloak-conf", Target = "/opt/keycloak/conf/keycloak.conf", Mode = 0444 });

            service.Command = ["start"];
            service.Deploy ??= new();
            service.Deploy.Replicas = 1; // Keycloak resource builder doesn't support ReplicaAnnotation yet

            var config = builder.ApplicationBuilder.Configuration;

            if (config["AdminEntrypoint"] is string adminEntryPoint
                && config["KeycloakDomain"] is string keycloakDomain
                && config["PublicEntrypoint"] is string publicEntryPoint)
                service.WithTraefikLabels(
                    FrontEndNetwork,
                    new(8080, true),
                    new TraefikRouterDef(adminEntryPoint, keycloakDomain),
                    new TraefikRouterDef(publicEntryPoint, keycloakDomain, ["/realms", "/resources"]));
        });


    public static IResourceBuilder<ProjectResource> ConfigureMigrationsForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .WithCommonOptions()
        .PublishAsDockerComposeService((res, service) =>
        {
            // Using secrets for connection strings
            service.Environment.Remove("ConnectionStrings__fiction-db");
            service.Environment.Remove("FICTION_DB_PASSWORD");
            service.Environment.Remove("FICTION_DB_URI");
            service.Environment.Remove("ConnectionStrings__stories-db");
            service.Environment.Remove("STORIES_DB_PASSWORD");
            service.Environment.Remove("STORIES_DB_URI");

            if (builder.ApplicationBuilder.Configuration["SecretsPath"] is string secretsPath)
                service.Environment["SecretsPath"] = secretsPath;

            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });
            service.Secrets.Add(new() { Source = "Dashboard__Otlp__PrimaryApiKey" });

            service.Deploy ??= new();
            // service.Deploy.Mode = "replicated-job"; // Swarm bug, just keeps restarting, so do 0 and then manual
            service.Deploy.Replicas = 0; // Replicated jobs run globally until this many successful completions
            service.Deploy.RestartPolicy = new()
            {
                Condition = "none",
                Delay = "120s",
                MaxAttempts = 1,
                Window = "60s"
            };

            if (builder.Resource.TryGetLastAnnotation<ContainerRegistryReferenceAnnotation>(out var reference))
                service.Image = $"{reference.ToRegistryStringAsync().GetAwaiter().GetResult()}/{res.Name}:latest";
        });

    public static IResourceBuilder<ProjectResource> ConfigureFictionApiForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .WithCommonOptions()
        .WithDockerHealthcheck()
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .PublishAsDockerComposeService((res, service) =>
        {
            // Using secrets for connection strings
            service.Environment.Remove("ConnectionStrings__fiction-db");
            service.Environment.Remove("FICTION_DB_PASSWORD");
            service.Environment.Remove("FICTION_DB_URI");
            service.Environment.Remove("ConnectionStrings__stories-db");
            service.Environment.Remove("STORIES_DB_PASSWORD");
            service.Environment.Remove("STORIES_DB_URI");

            var config = builder.ApplicationBuilder.Configuration;

            if (config["Api:AllowedHosts"] is string allowedHosts)
                service.Environment["AllowedHosts"] = allowedHosts;

            if (config["Api:AllowedOrigins"] is string allowedOrigins)
                service.Environment["AllowedOrigins"] = allowedOrigins;

            if (config["ApiBaseAddress"] is string apiBaseAddress)
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
            service.AddGracefulUpdate();

            service.Networks.Add(FrontEndNetwork);
            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });
            service.Secrets.Add(new() { Source = "Dashboard__Otlp__PrimaryApiKey" });
            service.Secrets.Add(new() { Source = "KeycloakAdminClientOptions__AuthClientSecret" });

            if (builder.Resource.TryGetLastAnnotation<ContainerRegistryReferenceAnnotation>(out var reference))
                service.Image = $"{reference.ToRegistryStringAsync().GetAwaiter().GetResult()}/{res.Name}:latest";

            if (config["PublicEntrypoint"] is string publicEntryPoint
                && config["ApiDomain"] is string apiDomain)
                service.WithTraefikLabels(
                    FrontEndNetwork,
                    new(8080),
                    new TraefikRouterDef(publicEntryPoint, apiDomain));
        });

    public static IResourceBuilder<ProjectResource> ConfigureWebClientForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .WithCommonOptions()
        .WithDockerHealthcheck()
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .PublishAsDockerComposeService((res, service) =>
        {
            service.Environment.Remove("ConnectionStrings__fiction-db");
            service.Environment.Remove("FICTION_DB_PASSWORD");
            service.Environment.Remove("FICTION_DB_URI");
            service.Environment.Remove("FICTION_HTTPS");

            var config = builder.ApplicationBuilder.Configuration;

            if (config["WebClient:AllowedHosts"] is string allowedHosts)
                service.Environment["AllowedHosts"] = allowedHosts;

            if (config["ApiBaseAddress"] is string apiBaseAddress)
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
            service.AddGracefulUpdate();

            service.Networks.Add(FrontEndNetwork);

            service.Secrets.Add(new() { Source = "Authentication__Schemes__Keycloak__ClientSecret" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "Dashboard__Otlp__PrimaryApiKey" });

            if (builder.Resource.TryGetLastAnnotation<ContainerRegistryReferenceAnnotation>(out var reference))
                service.Image = $"{reference.ToRegistryStringAsync().GetAwaiter().GetResult()}/{res.Name}:latest";

            if (config["PublicEntrypoint"] is string publicEntryPoint
                && config["WebClientDomain"] is string webClientDomain)
                service.WithTraefikLabels(
                    FrontEndNetwork,
                    new(8080),
                    new TraefikRouterDef(publicEntryPoint, webClientDomain));
        });
}