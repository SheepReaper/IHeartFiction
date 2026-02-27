#pragma warning disable ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREDOCKERFILEBUILDER001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPROBES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using IHFiction.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var keycloakClientSecret = builder.AddParameter(
    "ApiOidcClientSecret",
    secret: true);

var keycloakAdminClientSecret = builder.AddParameter(
    "ApiKeycloakAdminClientSecret",
    secret: true);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(options => options
        .WithImageTag("9.8")
        .WithLifetime(ContainerLifetime.Persistent));

var mongo = builder.AddMongoDB("mongo")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithMongoExpress(options => options
        .WithLifetime(ContainerLifetime.Persistent));

var storiesDb = mongo.AddDatabase("stories-db");

var fictionDb = postgres.AddDatabase("fiction-db");

var keycloak = builder.AddKeycloak("keycloak", builder.Environment.IsDevelopment() ? 8080 : null)
    .WithImageTag("26.3")
    .WithLifetime(ContainerLifetime.Persistent);

var migrations = builder.AddProject<Projects.IHFiction_MigrationService>("migrations")
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WaitFor(postgres)
    .WaitFor(storiesDb);

var fictionApi = builder.AddProject<Projects.IHFiction_FictionApi>("fiction")
    .WithDockerfileBaseImage(runtimeImage: "mcr.microsoft.com/dotnet/aspnet:10.0-alpine")
    .WithReference(keycloak)
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WaitFor(storiesDb)
    .WaitFor(fictionDb)
    .WithHttpProbe(ProbeType.Liveness, "/health", endpointName: "http")
    .WithReplicas(builder.Configuration.GetValue("Containers:Api:ReplicaCount", 1));

var webClient = builder.AddProject<Projects.IHFiction_WebClient>("web")
    .WithDockerfileBaseImage(runtimeImage: "mcr.microsoft.com/dotnet/aspnet:10.0-alpine")
    .WithHttpProbe(ProbeType.Liveness, "/health", endpointName: "http")
    .WithReference(fictionApi) // API client uses service discovery if ApiBaseAddress is not set
    .WithReference(fictionDb) // Blazor server-side uses db directly via service discovery
    .WithReference(keycloak) // Blazor server-side uses Keycloak directly via service discovery
    .WithReplicas(builder.Configuration.GetValue("Containers:WebClient:ReplicaCount", 1));

if (builder.Environment.IsDevelopment())
{
    postgres.WithDataVolume("postgres-data");

    mongo.WithDataVolume("mongo-data");

    keycloak.WithDataVolume("keycloak-data")
        .WithRealmImport("../../../config/fiction-realm.json");

    fictionApi.WithEnvironment("KeycloakAdminClientOptions__AuthClientSecret", keycloakAdminClientSecret);
    webClient.WithEnvironment("Authentication__Schemes__Keycloak__ClientSecret", keycloakClientSecret);

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

if (builder.Environment.IsProduction())
{
    var registryUri = builder.AddParameter("RegistryUri");
    var repository = builder.AddParameter("Repository");

    var registry = builder.AddContainerRegistry("registry", registryUri, repository);

    builder.ConfigureSwarmCompose();

    postgres.ConfigureForSwarm();
    mongo.ConfigureForSwarm();
    keycloak.ConfigureForSwarm();

    migrations
        .WithContainerRegistry(registry)
        .ConfigureMigrationsForSwarm();

    fictionApi
        .WithContainerRegistry(registry)
        .ConfigureFictionApiForSwarm();

    webClient
        .WithContainerRegistry(registry)
        .ConfigureWebClientForSwarm();
}

await builder.Build().RunAsync();
