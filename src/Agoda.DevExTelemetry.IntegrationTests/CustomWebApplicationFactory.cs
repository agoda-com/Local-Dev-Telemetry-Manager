using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agoda.DevExTelemetry.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DatabaseProvider _provider;
    private SqliteConnection? _sqliteConnection;
    private string? _pgDatabaseName;

    public CustomWebApplicationFactory(DatabaseProvider provider = DatabaseProvider.Sqlite)
    {
        _provider = provider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_provider == DatabaseProvider.Sqlite)
        {
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            _sqliteConnection.Open();
        }
        else
        {
            _pgDatabaseName = $"test_{Guid.NewGuid():N}";
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
                services.AddDbContext<TelemetryDbContext>(options =>
                    options.UseSqlite(_sqliteConnection!));
                services.AddScoped<ITelemetryRepository, SqliteTelemetryRepository>();
            }
            else
            {
                var connStr = PostgresTestServer
                    .GetConnectionStringAsync(_pgDatabaseName!)
                    .GetAwaiter().GetResult();
                services.AddDbContext<TelemetryDbContext>(options =>
                    options.UseNpgsql(connStr));
                services.AddScoped<ITelemetryRepository, PostgresTelemetryRepository>();
            }
        });

        builder.UseEnvironment("Development");
    }

    public TelemetryDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    }

    public async Task DrainBackgroundQueuesAsync(CancellationToken cancellationToken = default)
    {
        var testRunQueue = Services.GetRequiredService<IBackgroundTaskQueue<IngestTestRunWorkItem>>();
        var buildMetricQueue = Services.GetRequiredService<IBackgroundTaskQueue<IngestBuildMetricWorkItem>>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        await Task.WhenAll(
            testRunQueue.WaitUntilDrainedAsync(cts.Token),
            buildMetricQueue.WaitUntilDrainedAsync(cts.Token));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _sqliteConnection?.Dispose();

            if (_pgDatabaseName != null)
                PostgresTestServer.DropDatabaseAsync(_pgDatabaseName).GetAwaiter().GetResult();
        }
    }
}
