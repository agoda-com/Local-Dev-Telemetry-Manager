using System;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.WebApi.Services;

public class TestRunIngestQueue : QueuedHostedService<IngestTestRunWorkItem>
{
    private readonly IServiceProvider _serviceProvider;

    public TestRunIngestQueue(
        IBackgroundTaskQueue<IngestTestRunWorkItem> taskQueue,
        IServiceProvider serviceProvider,
        ILogger<TestRunIngestQueue> logger)
        : base(taskQueue, logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ProcessWorkItem(
        Func<CancellationToken, IngestTestRunWorkItem> message, CancellationToken stoppingToken)
    {
        var workItem = message(stoppingToken);

        using var scope = _serviceProvider.CreateScope();
        var ingestService = scope.ServiceProvider.GetRequiredService<IIngestService>();

        await ingestService.IngestTestRunAsync(workItem.TestRun, workItem.TestCases);

        if (workItem.RawPayloadJson != null && workItem.RawPayloadEndpoint != null)
        {
            await ingestService.StoreRawPayloadAsync(
                workItem.RawPayloadEndpoint,
                workItem.RawPayloadContentType ?? "application/json",
                workItem.RawPayloadJson);
        }
    }
}
