using System.Threading.Channels;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IBackgroundTaskQueue<T>
{
    Task QueueBackgroundWorkItemAsync(T workItem);
    Task<T> DequeueAsync(CancellationToken cancellationToken);
    void NotifyItemProcessed();
    Task WaitUntilDrainedAsync(CancellationToken cancellationToken = default);
}

[RegisterSingleton(For = typeof(IBackgroundTaskQueue<>))]
public class BackgroundTaskQueue<T> : IBackgroundTaskQueue<T>
{
    private readonly Channel<T> _queue =
        Channel.CreateBounded<T>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    private int _outstandingCount;

    public async Task QueueBackgroundWorkItemAsync(T workItem)
    {
        Interlocked.Increment(ref _outstandingCount);
        try
        {
            await _queue.Writer.WriteAsync(workItem);
        }
        catch
        {
            Interlocked.Decrement(ref _outstandingCount);
            throw;
        }
    }

    public async Task<T> DequeueAsync(CancellationToken cancellationToken)
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
