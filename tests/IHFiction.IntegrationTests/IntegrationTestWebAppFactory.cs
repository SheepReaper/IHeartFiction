using System.Reflection;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using MongoDB.Driver;

using Npgsql;

using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(IHFiction.IntegrationTests.IntegrationTestWebAppFactory))]

namespace IHFiction.IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    const string PgContainerName = "ihfiction-postgres-tests";
    const string MongoContainerName = "ihfiction-mongo-tests";
    const string RedisContainerName = "ihfiction-redis-tests";
    const ushort RedisPort = 6379;
    private readonly PostgreSqlContainer _pgContainer = new PostgreSqlBuilder("library/postgres:17.4")
        .WithDatabase("fiction-db")
        .WithName(PgContainerName)
        .WithReuse(false)
        .WithLabel("reuse-id", PgContainerName) // Explicit name for reuse
        .Build();

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("library/mongo:8.0")
        .WithName(MongoContainerName)
        .WithReuse(false)
        .WithLabel("reuse-id", MongoContainerName) // Explicit name for reuse
        .Build();

    private readonly IContainer _redisContainer = new ContainerBuilder("redis:8.2-alpine")
        .WithName(RedisContainerName)
        .WithPortBinding(RedisPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
        .Build();

    public string PostgreSqlConnectionString => _pgContainer.GetConnectionString();

    public NpgsqlConnectionStringBuilder GetNpgsqlConnectionStringBuilder() => new(_pgContainer.GetConnectionString());

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _pgContainer.StartAsync(TestContext.Current.CancellationToken);
        await _mongoContainer.StartAsync(TestContext.Current.CancellationToken);
        await _redisContainer.StartAsync(TestContext.Current.CancellationToken);
        await CleanupLeftoverTestDatabasesAsync();
    }

    private async Task CleanupLeftoverTestDatabasesAsync()
    {
        var connectionProvider = new PgsqlConnectionStringProvider(_pgContainer.GetConnectionString());
        var masterConnectionString = new NpgsqlConnectionStringBuilder(_pgContainer.GetConnectionString())
        {
            Database = "postgres",
            Pooling = false
        }.ToString();

        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // Find all test databases (those starting with "test_")
        var findTestDbsSql = """
            SELECT datname FROM pg_database
            WHERE datname LIKE 'test_%' AND datistemplate = false;
            """;

        await using var command = new NpgsqlCommand(findTestDbsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var testDatabases = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            testDatabases.Add(reader.GetString(0));
        }
        await reader.CloseAsync();

        // Clean up each test database
        foreach (var dbName in testDatabases)
        {
            await connectionProvider.CleanupDatabaseAsync(dbName, TestContext.Current.CancellationToken);
        }

        // Do the same for mongo
        var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
        var mongoDatabases = await mongoClient.ListDatabasesAsync(TestContext.Current.CancellationToken);
        foreach (var db in await mongoDatabases.ToListAsync(TestContext.Current.CancellationToken))
        {
            var databaseName = db["name"].AsString;
            if (databaseName.StartsWith("test_"))
            {
                await mongoClient.DropDatabaseAsync(databaseName, TestContext.Current.CancellationToken);
            }
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Stop Wolverine's background durability agents before their PostgreSQL
        // and Redis dependencies are torn down.
        await base.DisposeAsync();
        await _pgContainer.StopAsync(TestContext.Current.CancellationToken);
        await _mongoContainer.StopAsync(TestContext.Current.CancellationToken);
        await _redisContainer.StopAsync(TestContext.Current.CancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:fiction-db", _pgContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:stories-db", _mongoContainer.GetConnectionString());
        builder.UseSetting(
            "ConnectionStrings:redis",
            $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(RedisPort)},abortConnect=false");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(new PgsqlConnectionStringProvider(_pgContainer.GetConnectionString()));

            var configureServiceTypes = Assembly
                .GetExecutingAssembly()
                .DefinedTypes
                .Where(t => t is { IsAbstract: false, IsInterface: false })
                .Where(t => t.GetInterfaces()
                    .Any(i => i.IsGenericType &&
                             i.GetGenericTypeDefinition() == typeof(IConfigureServices<>) &&
                             i.GetGenericArguments()[0] == t.AsType()));

            foreach (var type in configureServiceTypes)
            {
                type.AsType().GetMethod(nameof(IConfigureServices<>.ConfigureServices))?.Invoke(null, [services]);
            }
        });
    }
}
