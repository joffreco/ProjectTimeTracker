using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Infrastructure;

public sealed class BackgroundSyncWorker : IDisposable
{
    private readonly IEventQueue _queue;
    private readonly FirestoreEventGateway _eventGateway;
    private readonly TimeSpan _idleDelay = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _maxBackoff = TimeSpan.FromMinutes(2);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public BackgroundSyncWorker(IEventQueue queue, FirestoreEventGateway eventGateway)
    {
        _queue = queue;
        _eventGateway = eventGateway;
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null || _loopTask is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<QueuedStateEvent> due = await _queue.GetDueAsync(DateTime.UtcNow, 20, cancellationToken);
            if (due.Count == 0)
            {
                await Task.Delay(_idleDelay, cancellationToken);
                continue;
            }

            foreach (QueuedStateEvent item in due)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await _eventGateway.AppendAsync(item.Event, cancellationToken);
                    await _queue.MarkDispatchedAsync(item.LocalId, cancellationToken);
                }
                catch (Exception ex)
                {
                    int nextAttempt = item.AttemptCount + 1;
                    TimeSpan delay = ComputeBackoff(nextAttempt);
                    await _queue.MarkFailedAsync(
                        item.LocalId,
                        nextAttempt,
                        DateTime.UtcNow.Add(delay),
                        ex.Message,
                        cancellationToken);
                }
            }
        }
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        int exponent = Math.Min(8, Math.Max(0, attempt));
        double seconds = Math.Pow(2, exponent);
        double jitter = Random.Shared.NextDouble() * 0.5;
        double total = Math.Min(_maxBackoff.TotalSeconds, seconds + jitter);
        return TimeSpan.FromSeconds(total);
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}

