using System;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.WebApi.Services;

public class BuildMetricIngestQueue : QueuedHostedService<IngestBuildMetricWorkItem>
{
    private readonly IServiceProvider _serviceProvider;

    public BuildMetricIngestQueue(
        IBackgroundTaskQueue<IngestBuildMetricWorkItem> taskQueue,
        IServiceProvider serviceProvider,
        ILogger<BuildMetricIngestQueue> logger)
        : base(taskQueue, logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ProcessWorkItem(
        Func<CancellationToken, IngestBuildMetricWorkItem> message, CancellationToken stoppingToken)
    {
        var workItem = message(stoppingToken);

        using var scope = _serviceProvider.CreateScope();
        var ingestService = scope.ServiceProvider.GetRequiredService<IIngestService>();

        await ingestService.IngestBuildMetricAsync(workItem.BuildMetric);

        if (workItem.RawPayloadJson != null && workItem.RawPayloadEndpoint != null)
        {
            await ingestService.StoreRawPayloadAsync(
                workItem.RawPayloadEndpoint,
                workItem.RawPayloadContentType ?? "application/json",
                workItem.RawPayloadJson);
        }
    }
}
