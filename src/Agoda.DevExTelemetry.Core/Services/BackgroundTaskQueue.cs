using System.Threading.Channels;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IBackgroundTaskQueue<T>
{
    Task QueueBackgroundWorkItemAsync(Func<CancellationToken, T> workItem);
    Task<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue<T> : IBackgroundTaskQueue<T>
{
    private readonly Channel<Func<CancellationToken, T>> _queue =
        Channel.CreateUnbounded<Func<CancellationToken, T>>();

    public async Task QueueBackgroundWorkItemAsync(Func<CancellationToken, T> workItem)
        => await _queue.Writer.WriteAsync(workItem);

    public async Task<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken)
        => await _queue.Reader.ReadAsync(cancellationToken);
}
