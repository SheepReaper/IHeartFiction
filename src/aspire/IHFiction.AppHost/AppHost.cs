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
    .WithReference(keycloak)
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WaitFor(storiesDb)
    .WaitFor(fictionDb)
    .WithHttpHealthCheck("/health")
    .WithReplicas(builder.Configuration.GetValue("Containers:Api:ReplicaCount", 1));

var webClient = builder.AddProject<Projects.IHFiction_WebClient>("web")
    .WithHttpHealthCheck("/health")
    .WithReference(fictionDb)
    .WithReference(keycloak)
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
    builder.ConfigureSwarmCompose();

    postgres.ConfigureForSwarm();
    mongo.ConfigureForSwarm();
    keycloak.ConfigureForSwarm();

    migrations.WithImageRegistry()
        .PushToRegistry()
        .ConfigureMigrationsForSwarm();

    fictionApi.WithImageRegistry()
        .PushToRegistry()
        .ConfigureFictionApiForSwarm();

    webClient.WithImageRegistry()
        .PushToRegistry()
        .ConfigureWebClientForSwarm();
}

await builder.Build().RunAsync();
