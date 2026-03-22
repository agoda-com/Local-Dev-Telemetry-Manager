using Npgsql;
using Testcontainers.PostgreSql;

namespace Agoda.DevExTelemetry.IntegrationTests;

public static class PostgresTestServer
{
    private static PostgreSqlContainer? _container;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static async Task<string> GetConnectionStringAsync(string databaseName)
    {
        await EnsureStartedAsync();
        var builder = new NpgsqlConnectionStringBuilder(_container!.GetConnectionString())
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }

    public static async Task CreateDatabaseAsync(string databaseName)
    {
        await EnsureStartedAsync();
        await using var conn = new NpgsqlConnection(_container!.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureStartedAsync()
    {
        if (_container != null) return;
        await _semaphore.WaitAsync();
        try
        {
            if (_container != null) return;
            _container = new PostgreSqlBuilder("postgres:16-alpine")
                .Build();
            await _container.StartAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
