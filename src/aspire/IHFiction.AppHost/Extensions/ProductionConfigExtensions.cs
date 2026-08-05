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
        });

    const string AdminNetwork = "t3_proxy";
    const string ContainerNetwork = "containers";
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
                Name = AdminNetwork,
                External = true
            })
            .AddNetwork(new()
            {
                Name = ContainerNetwork,
                Internal = false
            });

            file.Secrets.Add("keycloak-conf", new() { File = $"{SecretsPath}/keycloak.conf" });
            file.Secrets.Add("cloudflared-tunnel-token", new() { File = $"{SecretsPath}/cloudflared-tunnel-token.secret" });
            file.Secrets.Add("mongodb-root-pass", new() { File = $"{SecretsPath}/mongodb-root-pass.secret" });
            file.Secrets.Add("postgres-pass", new() { File = $"{SecretsPath}/postgres-pass.secret" });
            file.Secrets.Add("redis-pass", new() { File = $"{SecretsPath}/redis-pass.secret" });

            file.Secrets.Add("ConnectionStrings__fiction-db", new() { File = $"{SecretsPath}/conn-fiction-db.secret" });
            file.Secrets.Add("ConnectionStrings__redis", new() { File = $"{SecretsPath}/conn-redis.secret" });
            file.Secrets.Add("ConnectionStrings__stories-db", new() { File = $"{SecretsPath}/conn-stories-db.secret" });

            file.Secrets.Add("Authentication__Schemes__Keycloak__ClientSecret", new() { File = $"{SecretsPath}/keycloak-frontend-client.secret" });
            file.Secrets.Add("KeycloakAdminClientOptions__AuthClientSecret", new() { File = $"{SecretsPath}/keycloak-admin-client.secret" });

            file.Secrets.Add("WebPush__PrivateKey", new() { File = $"{SecretsPath}/vapid-private-key.secret" });
            file.Secrets.Add("WebPush__PublicKey", new() { File = $"{SecretsPath}/vapid-public-key.secret" });

            file.Secrets.Add("Dashboard__Otlp__PrimaryApiKey", new() { File = $"{SecretsPath}/otlp-api-key.secret" });

            var tunnel = file.Services["ihfiction-tunnel"];
            tunnel.Environment.Remove("TUNNEL_TOKEN");
            tunnel.Command.Add("--token-file");
            tunnel.Command.Add("/run/secrets/cloudflared-tunnel-token");
            tunnel.Secrets.Add(new() { Source = "cloudflared-tunnel-token" });
            tunnel.Deploy ??= new();
            tunnel.Deploy.Replicas = 2;
            tunnel.AddGracefulUpdate();

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
            file.Remove("IHFICTION_TUNNEL_TUNNEL_TOKEN");
            file.Remove("KEYCLOAK_PASSWORD");
            file.Remove("MIGRATIONS_IMAGE");
            file.Remove("MONGO_PASSWORD");
            file.Remove("POSTGRES_PASSWORD");
            file.Remove("REDIS_PASSWORD");
            file.Remove("VAPIDPRIVKEY");
            file.Remove("VAPIDPUBKEY");
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
                Source = $"{DataPath}/postgres18-20260804-003207",
                Target = "/var/lib/postgresql",
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

    public static IResourceBuilder<RedisResource> ConfigureForSwarm(this IResourceBuilder<RedisResource> builder) => builder
        .WithDockerHealthcheck(["CMD-SHELL", "redis-cli -a \"$$(cat /run/secrets/redis-pass)\" ping"])
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Environment.Remove("REDIS_PASSWORD");
            service.Command = ["-c", "redis-server --requirepass \"$$(cat /run/secrets/redis-pass)\""];
            service.Secrets.Add(new() { Source = "redis-pass" });

            service.AddVolume(new()
            {
                Name = "redis-data",
                Type = "bind",
                Source = $"{DataPath}/redis",
                Target = "/data"
            });
        });

    public static IResourceBuilder<KeycloakResource> ConfigureForSwarm(this IResourceBuilder<KeycloakResource> builder) => builder
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .WithDockerHealthcheck(
            ["CMD-SHELL", "{ printf 'HEAD /health/ready HTTP/1.0\r\n\r\n' >&0; grep 'HTTP/1.0 200'; } 0<>/dev/tcp/localhost/9000"],
            options => options.StartPeriodSeconds = 120)
        .PublishAsDockerComposeService((_, service) =>
        {
            // Using conf file
            service.Environment.Remove("KC_BOOTSTRAP_ADMIN_PASSWORD");

            service.Environment["JAVA_OPTS_APPEND"] = "-Djgroups.bind.address=match-interface:eth2";

            service.Secrets.Add(new() { Source = "keycloak-conf", Target = "/opt/keycloak/conf/keycloak.conf", Mode = 0444 });

            service.Command = ["start"];
            service.Deploy ??= new();
            service.Deploy.Replicas = 1; // Keycloak resource builder doesn't support ReplicaAnnotation yet

            var config = builder.ApplicationBuilder.Configuration;

            if (config["AdminEntrypoint"] is string adminEntryPoint
                && config["KeycloakDomain"] is string keycloakDomain)
                service.WithTraefikLabels(
                    AdminNetwork,
                    new(8080, true),
                    new TraefikRouterDef(adminEntryPoint, keycloakDomain));
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
        .WithDockerHealthcheck(configureOptions: options =>
        {
            options.IntervalSeconds = 15;
            options.TimeoutSeconds = 5;
            options.StartPeriodSeconds = 300;
            options.Retries = 3;
        })
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
            service.Environment.Remove("ConnectionStrings__redis");
            service.Environment.Remove("REDIS_HOST");
            service.Environment.Remove("REDIS_PASSWORD");
            service.Environment.Remove("REDIS_PORT");
            service.Environment.Remove("REDIS_URI");
            service.Environment.Remove("WebPush__PrivateKey");
            service.Environment.Remove("WebPush__PublicKey");

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

            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__redis" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });
            service.Secrets.Add(new() { Source = "Dashboard__Otlp__PrimaryApiKey" });
            service.Secrets.Add(new() { Source = "KeycloakAdminClientOptions__AuthClientSecret" });
            service.Secrets.Add(new() { Source = "WebPush__PrivateKey" });
            service.Secrets.Add(new() { Source = "WebPush__PublicKey" });

            if (builder.Resource.TryGetLastAnnotation<ContainerRegistryReferenceAnnotation>(out var reference))
                service.Image = $"{reference.ToRegistryStringAsync().GetAwaiter().GetResult()}/{res.Name}:latest";
        });

    public static IResourceBuilder<ProjectResource> ConfigureWebClientForSwarm(this IResourceBuilder<ProjectResource> builder) => builder
        .WithCommonOptions()
        .WithDockerHealthcheck(configureOptions: options =>
        {
            options.IntervalSeconds = 15;
            options.TimeoutSeconds = 5;
            options.StartPeriodSeconds = 300;
            options.Retries = 3;
        })
        .WithEndpoint("http", e => e.TargetPort = 8080)
        .PublishAsDockerComposeService((res, service) =>
        {
            service.Environment.Remove("ConnectionStrings__fiction-db");
            service.Environment.Remove("FICTION_DB_PASSWORD");
            service.Environment.Remove("FICTION_DB_URI");
            service.Environment.Remove("FICTION_HTTPS");
            service.Environment.Remove("WebPush__PublicKey");

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

            service.Secrets.Add(new() { Source = "Authentication__Schemes__Keycloak__ClientSecret" });
            service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
            service.Secrets.Add(new() { Source = "Dashboard__Otlp__PrimaryApiKey" });
            service.Secrets.Add(new() { Source = "WebPush__PublicKey" });

            if (builder.Resource.TryGetLastAnnotation<ContainerRegistryReferenceAnnotation>(out var reference))
                service.Image = $"{reference.ToRegistryStringAsync().GetAwaiter().GetResult()}/{res.Name}:latest";
        });
}
