using System;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.WebApi.Services;

public abstract class QueuedHostedService<T> : BackgroundService
{
    private IBackgroundTaskQueue<T> TaskQueue { get; }
    protected ILogger Logger { get; }

    protected QueuedHostedService(IBackgroundTaskQueue<T> taskQueue, ILogger logger)
    {
        TaskQueue = taskQueue;
        Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Queued Hosted Service for {ServiceName} is running", typeof(T).Name);
        await BackgroundProcessing(stoppingToken);
    }

    protected abstract Task ProcessWorkItem(Func<CancellationToken, T> message, CancellationToken stoppingToken);

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await TaskQueue.DequeueAsync(stoppingToken);
            try
            {
                await ProcessWorkItem(workItem, stoppingToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred executing {WorkItem}", nameof(workItem));
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Queued Hosted Service for {ServiceName} is stopping", typeof(T).Name);
        await base.StopAsync(stoppingToken);
    }
}
