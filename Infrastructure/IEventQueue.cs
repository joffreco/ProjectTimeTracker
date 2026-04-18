using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Infrastructure;

public sealed record QueuedStateEvent(
    long LocalId,
    StateEvent Event,
    int AttemptCount,
    DateTime NextAttemptUtc,
    string? LastError);

public interface IEventQueue
{
    Task EnqueueAsync(StateEvent stateEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueuedStateEvent>> GetDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken);

    Task MarkDispatchedAsync(long localId, CancellationToken cancellationToken);

    Task MarkFailedAsync(long localId, int attemptCount, DateTime nextAttemptUtc, string error, CancellationToken cancellationToken);
}

