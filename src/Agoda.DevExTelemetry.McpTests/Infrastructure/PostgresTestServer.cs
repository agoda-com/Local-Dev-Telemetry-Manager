using Npgsql;
using Testcontainers.PostgreSql;

namespace Agoda.DevExTelemetry.McpTests.Infrastructure;

/// <summary>
/// The PostgreSQL server for the PostgreSql fixtures. CI starts a Testcontainers container. On a machine
/// without Docker, set <c>DEVEX_TEST_POSTGRES</c> to the connection string of a running server (no
/// database name needed); each factory creates and drops its own database there.
/// </summary>
public static class PostgresTestServer
{
    public const string SERVER_ENV_VAR = "DEVEX_TEST_POSTGRES";

    private static PostgreSqlContainer? _container;
    private static string? _serverConnectionString;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public static async Task<string> GetConnectionStringAsync(string databaseName)
    {
        await EnsureStartedAsync();
        var builder = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = databaseName };
        return builder.ConnectionString;
    }

    public static async Task CreateDatabaseAsync(string databaseName)
    {
        await EnsureStartedAsync();
        await using var conn = new NpgsqlConnection(_serverConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabaseAsync(string databaseName)
    {
        await EnsureStartedAsync();
        await using var conn = new NpgsqlConnection(_serverConnectionString);
        await conn.OpenAsync();

        await using var terminateCmd = conn.CreateCommand();
        terminateCmd.CommandText = $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{databaseName}' AND pid <> pg_backend_pid()
            """;
        await terminateCmd.ExecuteNonQueryAsync();

        await using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await dropCmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureStartedAsync()
    {
        if (_serverConnectionString != null) return;
        await Semaphore.WaitAsync();
        try
        {
            if (_serverConnectionString != null) return;

            var external = Environment.GetEnvironmentVariable(SERVER_ENV_VAR);
            if (!string.IsNullOrWhiteSpace(external))
            {
                _serverConnectionString = new NpgsqlConnectionStringBuilder(external) { Database = "postgres" }.ConnectionString;
                return;
            }

            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            _serverConnectionString = _container.GetConnectionString();
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
