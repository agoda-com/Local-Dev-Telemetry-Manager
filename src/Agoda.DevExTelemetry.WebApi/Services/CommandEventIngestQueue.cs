using System;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Agoda.IoC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.WebApi.Services;

[RegisterSingleton(For = typeof(IHostedService))]
public class CommandEventIngestQueue : QueuedHostedService<IngestCommandEventWorkItem>
{
    private readonly IServiceProvider _serviceProvider;

    public CommandEventIngestQueue(
        IBackgroundTaskQueue<IngestCommandEventWorkItem> taskQueue,
        IServiceProvider serviceProvider,
        ILogger<CommandEventIngestQueue> logger)
        : base(taskQueue, logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ProcessWorkItem(
        IngestCommandEventWorkItem workItem, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var ingestService = scope.ServiceProvider.GetRequiredService<IIngestService>();

        await ingestService.IngestCommandEventAsync(workItem.CommandEvent, workItem.NpmTimers);

        if (workItem.RawPayloadJson != null && workItem.RawPayloadEndpoint != null)
        {
            await ingestService.StoreRawPayloadAsync(
                workItem.RawPayloadEndpoint,
                workItem.RawPayloadContentType ?? "application/json",
                workItem.RawPayloadJson);
        }
    }
}
