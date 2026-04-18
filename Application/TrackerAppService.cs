using ProjectTimeTracker.Domain;
using ProjectTimeTracker.Infrastructure;

namespace ProjectTimeTracker.Application;

public sealed class TrackerAppService : IDisposable
{
    private readonly IGoogleFirestoreSession _session;
    private readonly FirestoreEventGateway _gateway;
    private readonly IEventQueue _queue;
    private readonly BackgroundSyncWorker _syncWorker;
    private readonly DeviceIdentityProvider _deviceIdentityProvider;

    private readonly HashSet<Guid> _seenEvents = [];
    private readonly object _stateLock = new();

    private string? _userId;
    private string? _deviceId;
    private long _deviceSequence;

    public TrackerAppService(
        IGoogleFirestoreSession session,
        FirestoreEventGateway gateway,
        IEventQueue queue,
        BackgroundSyncWorker syncWorker,
        DeviceIdentityProvider deviceIdentityProvider)
    {
        _session = session;
        _gateway = gateway;
        _queue = queue;
        _syncWorker = syncWorker;
        _deviceIdentityProvider = deviceIdentityProvider;
    }

    public TrackerState CurrentState { get; private set; } = TrackerState.Empty;

    public bool IsConnected => _session.IsConnected && !string.IsNullOrWhiteSpace(_userId);

    public event Action<TrackerState, StateEvent, bool>? StateChanged;

    public async Task ConnectAsync(string secretPath, string userId, CancellationToken cancellationToken)
    {
        _userId = userId;
        _deviceId = _deviceIdentityProvider.GetOrCreateDeviceId();

        await _session.ConnectAsync(secretPath, cancellationToken);
        _gateway.ConfigureUser(userId);

        IReadOnlyList<StateEvent> existingEvents = await _gateway.ReadAllAsync(cancellationToken);
        foreach (StateEvent stateEvent in existingEvents)
        {
            ApplyIfNew(stateEvent, isLocal: false);
        }

        await _gateway.StartListeningAsync(
            onEvent: evt => ApplyIfNew(evt, isLocal: false),
            onError: _ => { },
            cancellationToken: cancellationToken);

        _syncWorker.Start();
    }

    public async Task<StateEvent> EmitIntentAsync(StateIntent intent, CancellationToken cancellationToken)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(_userId) || string.IsNullOrWhiteSpace(_deviceId))
        {
            throw new InvalidOperationException("Connect before changing state.");
        }

        TransitionValidator.Validate(CurrentState, intent);

        long sequence;
        lock (_stateLock)
        {
            _deviceSequence++;
            sequence = _deviceSequence;
        }

        StateEvent stateEvent = StateEvent.FromIntent(intent, _userId, _deviceId, sequence, DateTime.UtcNow);
        await _queue.EnqueueAsync(stateEvent, cancellationToken);
        ApplyIfNew(stateEvent, isLocal: true);

        return stateEvent;
    }

    private void ApplyIfNew(StateEvent stateEvent, bool isLocal)
    {
        lock (_stateLock)
        {
            if (!_seenEvents.Add(stateEvent.EventId))
            {
                return;
            }

            CurrentState = StateMachine.Apply(CurrentState, stateEvent);
        }

        StateChanged?.Invoke(CurrentState, stateEvent, isLocal);
    }

    public void Dispose()
    {
        _gateway.Dispose();
        _syncWorker.Dispose();
    }
}

