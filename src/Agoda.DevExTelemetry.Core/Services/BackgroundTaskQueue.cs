using System.Threading.Channels;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IBackgroundTaskQueue<T>
{
    Task QueueBackgroundWorkItemAsync(Func<CancellationToken, T> workItem);
    Task<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken);
    void NotifyItemProcessed();
    Task WaitUntilDrainedAsync(CancellationToken cancellationToken = default);
}

public class BackgroundTaskQueue<T> : IBackgroundTaskQueue<T>
{
    private readonly Channel<Func<CancellationToken, T>> _queue =
        Channel.CreateUnbounded<Func<CancellationToken, T>>();

    private int _outstandingCount;

    public async Task QueueBackgroundWorkItemAsync(Func<CancellationToken, T> workItem)
    {
        Interlocked.Increment(ref _outstandingCount);
        await _queue.Writer.WriteAsync(workItem);
    }

    public async Task<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken)
        => await _queue.Reader.ReadAsync(cancellationToken);

    public void NotifyItemProcessed()
        => Interlocked.Decrement(ref _outstandingCount);

    public async Task WaitUntilDrainedAsync(CancellationToken cancellationToken = default)
    {
        while (Volatile.Read(ref _outstandingCount) > 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
