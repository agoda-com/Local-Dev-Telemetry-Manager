using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterSingleton(For = typeof(IHostedService))]
public class DataCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataCleanupService> _logger;
    private readonly int _retentionDays;
    private readonly int _cleanupIntervalHours;
    private readonly int _vacuumIntervalDays;
    private DateTime _lastVacuumDate = DateTime.MinValue;

    public DataCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = int.TryParse(configuration["DataRetention:RetentionDays"], out var rd) ? rd : 90;
        _cleanupIntervalHours = int.TryParse(configuration["DataRetention:CleanupIntervalHours"], out var ci) ? ci : 24;
        _vacuumIntervalDays = int.TryParse(configuration["DataRetention:VacuumIntervalDays"], out var vi) ? vi : 7;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_cleanupIntervalHours));

        await RunCleanupAsync();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync();
        }
    }

    private async Task RunCleanupAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITelemetryRepository>();

            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            await repository.DeleteOldDataAsync(cutoff);

            _logger.LogInformation("Data cleanup complete");

            if (DateTime.UtcNow - _lastVacuumDate > TimeSpan.FromDays(_vacuumIntervalDays))
            {
                await repository.RunMaintenanceAsync();
                _lastVacuumDate = DateTime.UtcNow;
                _logger.LogInformation("Database maintenance completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data cleanup failed");
        }
    }
}
