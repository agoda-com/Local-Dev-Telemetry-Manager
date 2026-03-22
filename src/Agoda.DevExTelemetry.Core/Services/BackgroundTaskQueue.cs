using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IBackgroundTaskQueue<T>
{
    Task QueueBackgroundWorkItemAsync(Func<CancellationToken, T> workItem);
    Task<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue<T> : IBackgroundTaskQueue<T>
{
    private readonly Channel<Func<CancellationToken, T>> _queue;

    public BackgroundTaskQueue()
    {
        _queue = Channel.CreateUnbounded<Func<CancellationToken, T>>();
    }

    public async Task QueueBackgroundWorkItemAsync(Func<CancellationToken, T> workItem)
    {
        await _queue.Writer.WriteAsync(workItem);
    }

    public async Task<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
