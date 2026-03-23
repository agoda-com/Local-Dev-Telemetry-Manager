using Agoda.DevExTelemetry.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agoda.DevExTelemetry.DbPerfTests;

public sealed class PerfWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DatabaseProvider _provider;
    private SqliteConnection? _sqliteConnection;
    private string? _pgDatabaseName;

    public PerfWebApplicationFactory(DatabaseProvider provider)
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
            _pgDatabaseName = $"perf_{Guid.NewGuid():N}";
            PostgresPerfServer.CreateDatabaseAsync(_pgDatabaseName).GetAwaiter().GetResult();
        }

        builder.ConfigureServices(services =>
        {
            var efDescriptors = services
                .Where(d =>
                    d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                    d.ServiceType == typeof(DbContextOptions<TelemetryDbContext>) ||
                    d.ServiceType == typeof(TelemetryDbContext))
                .ToList();
            foreach (var d in efDescriptors)
                services.Remove(d);

            services.RemoveAll<ITelemetryRepository>();

            if (_provider == DatabaseProvider.Sqlite)
            {
                services.AddDbContext<TelemetryDbContext>(o => o.UseSqlite(_sqliteConnection!));
                services.AddScoped<ITelemetryRepository, SqliteTelemetryRepository>();
            }
            else
            {
                var connectionString = PostgresPerfServer.GetConnectionStringAsync(_pgDatabaseName!).GetAwaiter().GetResult();
                services.AddDbContext<TelemetryDbContext>(o => o.UseNpgsql(connectionString));
                services.AddScoped<ITelemetryRepository, PostgresTelemetryRepository>();
            }
        });

        builder.UseEnvironment("Development");
    }

    public async Task SeedIfNeededAsync(PerfSeedOptions options, CancellationToken ct = default)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        await PerfDataSeeder.SeedAsync(db, options, ct);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _sqliteConnection?.Dispose();

        if (_pgDatabaseName != null)
            PostgresPerfServer.DropDatabaseAsync(_pgDatabaseName).GetAwaiter().GetResult();
    }
}
