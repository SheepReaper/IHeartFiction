using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("local")
    .WithDashboard();

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("ihfiction.apphost-7522e92271-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(options => options
        .WithImageTag("9.7")
        .WithLifetime(ContainerLifetime.Persistent));

var storiesDb = builder.AddMongoDB("mongo")
    .WithDataVolume("ihfiction.apphost-7522e922-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithMongoExpress(options => options
        .WithLifetime(ContainerLifetime.Persistent))
    .AddDatabase("stories-db");

var fictionDb = postgres.AddDatabase("fiction-db");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume("ihfiction.apphost-7522e92271-keycloak-data")
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
    .WithEndpoint("https", e => e.TargetHost = "0.0.0.0")
    .WithReference(keycloak)
    .WithReference(fictionDb)
    .WithReference(storiesDb)
    .WithEnvironment("KeycloakAdminClientOptions__AuthClientSecret", keycloakAdminClientSecret)
    .WaitFor(storiesDb)
    .WaitFor(fictionDb)
    .WithHttpHealthCheck("/health");

var webClient = builder.AddProject<Projects.IHFiction_WebClient>("web")
    .WithEndpoint("https", e => e.TargetHost = "0.0.0.0")
    .WithHttpHealthCheck("/health")
    .WithReference(fictionApi)
    .WithReference(keycloak)
    .WithEnvironment("Authentication__Schemes__Keycloak__ClientSecret", keycloakClientSecret);

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

await builder.Build().RunAsync();
