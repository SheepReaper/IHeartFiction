using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("local")
    .WithDashboard();

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("ihfiction.postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(options => options
        .WithImageTag("9.7")
        .WithLifetime(ContainerLifetime.Persistent));

var storiesDb = builder.AddMongoDB("mongo")
    .WithDataVolume("ihfiction.mongo-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithMongoExpress(options => options
        .WithLifetime(ContainerLifetime.Persistent))
    .AddDatabase("stories-db");

var fictionDb = postgres.AddDatabase("fiction-db");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume("ihfiction.keycloak-data")
    .WithRealmImport("../../../config/fiction-realm.json")
    .WithLifetime(ContainerLifetime.Persistent);

var migrations = builder.AddProject<Projects.IHFiction_MigrationService>("migrations")
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WaitFor(postgres)
    .WaitFor(storiesDb);

var keycloakClientSecret = builder.AddParameter(
    "KeycloakClientSecret",
    secret: true);

var keycloakAdminClientSecret = builder.AddParameter(
    "KeycloakAdminClientSecret",
    secret: true);

var fictionApi = builder.AddProject<Projects.IHFiction_FictionApi>("fiction")
    .WithReference(keycloak)
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WithEnvironment("KeycloakAdminClientOptions__AuthClientSecret", keycloakAdminClientSecret)
    .WaitFor(storiesDb)
    .WaitFor(fictionDb)
    .WithHttpHealthCheck("/health");

var webClient = builder.AddProject<Projects.IHFiction_WebClient>("web")
    .WithHttpHealthCheck("/health")
    .WithReference(fictionApi)
    .WithReference(keycloak)
    .WithEnvironment("Authentication__Schemes__Keycloak__ClientSecret", keycloakClientSecret);

if (builder.Environment.IsDevelopment())
{
    fictionApi.WithEndpoint("https", e => e.TargetHost = "localhost");
    webClient.WithEndpoint("https", e => e.TargetHost = "localhost");
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

await builder.Build().RunAsync();
