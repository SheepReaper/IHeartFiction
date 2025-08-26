using Npgsql;

namespace IHFiction.IntegrationTests;

internal class PgsqlConnectionStringProvider(string baseConnectionString)
{
    public string GetConnectionStringForDatabase(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName,
            // Disable connection pooling for test databases to prevent hanging
            Pooling = false,
            // Set shorter timeouts for faster failure detection
            CommandTimeout = 30,
            Timeout = 15
        };
        return builder.ToString();
    }

    public async Task CleanupDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        var masterConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = "postgres", // Connect to master database
            Pooling = false
        }.ToString();

        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Terminate all connections to the target database
        var terminateConnectionsSql = $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();
            """;

        await using var terminateCommand = new NpgsqlCommand(terminateConnectionsSql, connection);
        await terminateCommand.ExecuteNonQueryAsync(cancellationToken);

        // Drop the database
        var dropDatabaseSql = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
        await using var dropCommand = new NpgsqlCommand(dropDatabaseSql, connection);
        await dropCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}