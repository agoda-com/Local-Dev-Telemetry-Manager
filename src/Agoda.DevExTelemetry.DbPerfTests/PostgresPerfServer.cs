using Npgsql;
using Testcontainers.PostgreSql;

namespace Agoda.DevExTelemetry.DbPerfTests;

public static class PostgresPerfServer
{
    private static PostgreSqlContainer? _container;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public static async Task<string> GetAdminConnectionStringAsync()
    {
        var envConnection = Environment.GetEnvironmentVariable("PERF_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envConnection))
            return envConnection;

        await EnsureStartedAsync();
        return _container!.GetConnectionString();
    }

    public static async Task<string> GetConnectionStringAsync(string databaseName)
    {
        var admin = await GetAdminConnectionStringAsync();
        var builder = new NpgsqlConnectionStringBuilder(admin)
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }

    public static async Task CreateDatabaseAsync(string databaseName)
    {
        var admin = await GetAdminConnectionStringAsync();
        await using var conn = new NpgsqlConnection(admin);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabaseAsync(string databaseName)
    {
        var admin = await GetAdminConnectionStringAsync();
        await using var conn = new NpgsqlConnection(admin);
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
        if (_container != null)
            return;

        await Semaphore.WaitAsync();
        try
        {
            if (_container != null)
                return;

            _container = new PostgreSqlBuilder("postgres:16-alpine")
                .Build();
            await _container.StartAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
