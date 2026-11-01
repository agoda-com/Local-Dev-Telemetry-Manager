using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Client;

namespace Agoda.DevExTelemetry.McpTests.Infrastructure;

public enum DatabaseProvider
{
    Sqlite,
    PostgreSql
}

/// <summary>
/// The WebApi in memory, on a fresh SQLite or PostgreSQL database, with a fixed clock at <see cref="Now"/>.
/// Seed rows with <see cref="SeedAsync"/> (see <see cref="Seed"/>), then talk to <c>/mcp</c> through
/// <see cref="ConnectAsync"/> or to the exports through <see cref="CreateClient"/>.
/// </summary>
public sealed class McpTestHost : WebApplicationFactory<Program>
{
    /// <summary>The fixed "now" every test runs at: a Wednesday at noon UTC.</summary>
    public static readonly DateTime Now = new(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly DatabaseProvider _provider;
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private SqliteConnection? _sqliteConnection;
    private string? _pgDatabaseName;

    /// <param name="provider">The database to run on.</param>
    /// <param name="settings">Configuration overrides, for example <c>["Mcp:MaxRows"] = "5"</c>.</param>
    public McpTestHost(DatabaseProvider provider, IReadOnlyDictionary<string, string?>? settings = null)
    {
        _provider = provider;
        _settings = settings ?? new Dictionary<string, string?>();
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(Now));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);

        if (_provider == DatabaseProvider.Sqlite)
        {
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            _sqliteConnection.Open();
        }
        else
        {
            _pgDatabaseName = $"mcp_test_{Guid.NewGuid():N}";
            PostgresTestServer.CreateDatabaseAsync(_pgDatabaseName).GetAwaiter().GetResult();
        }

        builder.ConfigureServices(services =>
        {
            var efDescriptors = services
                .Where(d =>
                    d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                    d.ServiceType == typeof(DbContextOptions<TelemetryDbContext>) ||
                    d.ServiceType == typeof(TelemetryDbContext))
                .ToList();
            foreach (var d in efDescriptors) services.Remove(d);

            services.RemoveAll<ITelemetryRepository>();

            if (_provider == DatabaseProvider.Sqlite)
            {
                services.AddDbContext<TelemetryDbContext>(options => options.UseSqlite(_sqliteConnection!));
                services.AddScoped<ITelemetryRepository, SqliteTelemetryRepository>();
            }
            else
            {
                var connStr = PostgresTestServer.GetConnectionStringAsync(_pgDatabaseName!).GetAwaiter().GetResult();
                services.AddDbContext<TelemetryDbContext>(options => options.UseNpgsql(connStr));
                services.AddScoped<ITelemetryRepository, PostgresTelemetryRepository>();
            }

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>Adds test runs (with their cases) and build metrics, in one save.</summary>
    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case TestRun run: db.TestRuns.Add(run); break;
                case BuildMetric metric: db.BuildMetrics.Add(metric); break;
                case IEnumerable<TestRun> runs: db.TestRuns.AddRange(runs); break;
                case IEnumerable<BuildMetric> metrics: db.BuildMetrics.AddRange(metrics); break;
                default: throw new ArgumentException($"Cannot seed {entity.GetType().Name}");
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>An MCP client connected to <c>/mcp</c> over Streamable HTTP. Dispose it after the test.</summary>
    public async Task<McpClient> ConnectAsync()
    {
        var http = CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            http,
            loggerFactory: null,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _sqliteConnection?.Dispose();
        if (_pgDatabaseName != null)
            PostgresTestServer.DropDatabaseAsync(_pgDatabaseName).GetAwaiter().GetResult();
    }
}
