using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

int apiReplicaCount = builder.Configuration.GetValue("Containers:Api:ReplicaCount", 1);
int webClientReplicaCount = builder.Configuration.GetValue("Containers:WebClient:ReplicaCount", 1);

var keycloakClientSecret = builder.AddParameter(
    "ApiOidcClientSecret",
    secret: true);

var keycloakAdminClientSecret = builder.AddParameter(
    "ApiKeycloakAdminClientSecret",
    secret: true);

var compose = builder.AddDockerComposeEnvironment("internal")
    .WithDashboard();

var postgres = builder.AddPostgres("postgres")
    .WithDataBindMount(builder.Configuration["Containers:Postgres:DataPath"] ?? "../../../data/postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(options => options
        .WithImageTag("9.7")
        .WithLifetime(ContainerLifetime.Persistent));

var mongo = builder.AddMongoDB("mongo")
    .WithDataBindMount(builder.Configuration["Containers:Mongo:DataPath"] ?? "../../../data/mongo")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithMongoExpress(options => options
        .WithLifetime(ContainerLifetime.Persistent));

var storiesDb = mongo.AddDatabase("stories-db");

var fictionDb = postgres.AddDatabase("fiction-db");

var keycloak = builder.AddKeycloak("keycloak", builder.Configuration.GetValue("Containers:Keycloak:Port", 8080))
    .WithDataBindMount(builder.Configuration["Containers:Keycloak:DataPath"] ?? "../../../data/keycloak")
    .WithRealmImport(builder.Configuration["Containers:Keycloak:RealmImportPath"] ?? "../../../config/fiction-realm.json")
    .WithLifetime(ContainerLifetime.Persistent);

var migrations = builder.AddProject<Projects.IHFiction_MigrationService>("migrations")
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WaitFor(postgres)
    .WaitFor(storiesDb);

var fictionApi = builder.AddProject<Projects.IHFiction_FictionApi>("fiction")
    .WithReference(keycloak)
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WithEnvironment("KeycloakAdminClientOptions__AuthClientSecret", keycloakAdminClientSecret)
    .WaitFor(storiesDb)
    .WaitFor(fictionDb)
    .WithHttpHealthCheck("/health")
    .WithReplicas(apiReplicaCount);

var webClient = builder.AddProject<Projects.IHFiction_WebClient>("web")
    .WithHttpHealthCheck("/health")
    .WithReference(fictionApi)
    .WithReference(keycloak)
    .WithEnvironment("Authentication__Schemes__Keycloak__ClientSecret", keycloakClientSecret)
    .WithReplicas(webClientReplicaCount);

if (builder.Environment.IsDevelopment())
{
    migrations
        .WithExplicitStart();
}

if (!builder.Environment.IsDevelopment())
{
    fictionApi
        .WaitForCompletion(migrations);

    webClient
        .WaitFor(fictionApi)
        .WaitFor(keycloak);
}

// The following is environment specific configuration for docker compose publish. It won't work for you.
if (builder.Environment.IsProduction())
{
    compose.ConfigureComposeFile(file =>
    {
        file.AddNetwork(new()
        {
            Name = "t3_proxy",
            External = true
        })
        .AddNetwork(new()
        {
            Name = "containers"
        });

        file.Secrets.Add("keycloak-admin-pass", new() { File = "./secrets/keycloak-admin-pass.secret" });
        file.Secrets.Add("mongodb-root-pass", new() { File = "./secrets/mongodb-root-pass.secret" });
        file.Secrets.Add("postgres-pass", new() { File = "./secrets/postgres-pass.secret" });

        file.Secrets.Add("ConnectionStrings__fiction-db", new() { File = "./secrets/conn-fiction-db.secret" });
        file.Secrets.Add("ConnectionStrings__stories-db", new() { File = "./secrets/conn-stories-db.secret" });

        file.Secrets.Add("Authentication__Schemes__Keycloak__ClientSecret", new() { File = "./secrets/keycloak-frontend-client.secret" });
        file.Secrets.Add("KeycloakAdminClientOptions__AuthClientSecret", new() { File = "./secrets/keycloak-admin-client.secret" });
    })
    .WithProperties(props =>
    {
        props.DefaultNetworkName = "containers";
        props.DefaultContainerRegistry = builder.Configuration["Registry"];
    });

    postgres.PublishAsDockerComposeService((_, service) =>
    {
        service.Environment.Remove("POSTGRES_PASSWORD");
        service.Environment["POSTGRES_PASSWORD_FILE"] = "/run/secrets/postgres-pass";

        service.Secrets.Add(new() { Source = "postgres-pass" });
    });

    mongo.PublishAsDockerComposeService((_, service) =>
    {
        service.Environment.Remove("MONGO_INITDB_ROOT_PASSWORD");
        service.Environment["MONGO_INITDB_ROOT_PASSWORD_FILE"] = "/run/secrets/mongodb-root-pass";

        service.Secrets.Add(new() { Source = "mongodb-root-pass" });
    });

    keycloak.PublishAsDockerComposeService((_, service) =>
    {
        service.Environment.Remove("KC_BOOTSTRAP_ADMIN_PASSWORD");
        service.Environment["KC_BOOTSTRAP_ADMIN_PASSWORD_FILE"] = "/run/secrets/keycloak-admin-pass";

        service.Networks.Add("t3_proxy");
        service.Secrets.Add(new() { Source = "keycloak-admin-pass" });
    });

    migrations.PublishAsDockerComposeService((_, service) =>
    {
        service.Environment.Remove("ConnectionStrings__fiction-db");
        service.Environment.Remove("ConnectionStrings__stories-db");

        if (builder.Configuration["SecretsPath"] is string secretsPath)
            service.Environment["SecretsPath"] = secretsPath;

        service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
        service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });

        if (builder.Configuration["Registry"] is string registry)
            service.Image = $"{registry}/{service.Name}:latest";
    });

    if (fictionApi.Resource.TryGetEndpoints(out var endpoints))
    {
        foreach (var endpoint in endpoints)
        {
            if (endpoint.Name == "https") fictionApi.Resource.Annotations.Remove(endpoint);
            if (endpoint.Name == "http") endpoint.TargetPort = 8080;
        }
    }

    fictionApi.PublishAsDockerComposeService((_, service) =>
    {
        service.Environment.Remove("ConnectionStrings__fiction-db");
        service.Environment.Remove("ConnectionStrings__stories-db");
        service.Environment.Remove("KeycloakAdminClientOptions__AuthClientSecret");

        if (builder.Configuration["OidcAuthority"] is string authority)
            service.Environment["OidcAuthority"] = authority;

        if (builder.Configuration["SecretsPath"] is string secretsPath)
            service.Environment["SecretsPath"] = secretsPath;

        service.Deploy ??= new();
        // BUG: This should be set automatically via ReplicaAnnotation
        service.Deploy.Replicas = apiReplicaCount;
        service.Networks.Add("t3_proxy");
        service.Secrets.Add(new() { Source = "ConnectionStrings__fiction-db" });
        service.Secrets.Add(new() { Source = "ConnectionStrings__stories-db" });
        service.Secrets.Add(new() { Source = "KeycloakAdminClientOptions__AuthClientSecret" });

        if (builder.Configuration["Registry"] is string registry)
            service.Image = $"{registry}/{service.Name}:latest";
    });

    if (webClient.Resource.TryGetEndpoints(out endpoints))
    {
        foreach (var endpoint in endpoints)
        {
            if (endpoint.Name == "https") webClient.Resource.Annotations.Remove(endpoint);
            if (endpoint.Name == "http") endpoint.TargetPort = 8080;
        }
    }

    webClient.PublishAsDockerComposeService((_, service) =>
    {
        service.Environment.Remove("Authentication__Schemes__Keycloak__ClientSecret");

        if (builder.Configuration["OidcAuthority"] is string authority)
            service.Environment["OidcAuthority"] = authority;

        if (builder.Configuration["SecretsPath"] is string secretsPath)
            service.Environment["SecretsPath"] = secretsPath;

        service.Deploy ??= new();
        // BUG: This should be set automatically via ReplicaAnnotation
        service.Deploy.Replicas = webClientReplicaCount;
        service.Networks.Add("t3_proxy");
        service.Secrets.Add(new() { Source = "Authentication__Schemes__Keycloak__ClientSecret" });

        if (builder.Configuration["Registry"] is string registry)
            service.Image = $"{registry}/{service.Name}:latest";
    });
}

await builder.Build().RunAsync();
