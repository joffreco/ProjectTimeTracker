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

    private readonly Dictionary<Guid, StateEvent> _events = [];
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

    /// <summary>Snapshot of all known events keyed by EventId.</summary>
    public IReadOnlyDictionary<Guid, StateEvent> AllEvents
    {
        get
        {
            lock (_stateLock)
            {
                return new Dictionary<Guid, StateEvent>(_events);
            }
        }
    }

    /// <summary>Fired for every event ingestion (add or modify). isLocal=true when the local UI emitted it.</summary>
    public event Action<TrackerState, StateEvent, bool>? StateChanged;

    /// <summary>Fired whenever the underlying event store changes (add/modify/remove).</summary>
    public event Action? EventsChanged;

    public async Task ConnectAsync(string userId, CancellationToken cancellationToken)
    {
        _userId = userId;
        _deviceId = _deviceIdentityProvider.GetOrCreateDeviceId();

        await _session.ConnectAsync(cancellationToken);
        _gateway.ConfigureUser(userId);

        // One-time (idempotent) migration of events from the legacy per-user sub-collection
        // to the new top-level `timeTrackerEvents` collection. Runs on every connect; it's a
        // no-op once everything has been moved.
        await _gateway.MigrateLegacyEventsAsync(cancellationToken);

        IReadOnlyList<StateEvent> existingEvents = await _gateway.ReadAllAsync(cancellationToken);
        foreach (StateEvent stateEvent in existingEvents)
        {
            IngestEvent(stateEvent, isLocal: false);
        }

        await _gateway.StartListeningAsync(
            onEvent: evt => IngestEvent(evt, isLocal: false),
            onRemoved: id => IngestRemove(id),
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
        IngestEvent(stateEvent, isLocal: true);

        return stateEvent;
    }

    /// <summary>Adds a brand-new historical event at an arbitrary timestamp. Requires online connection.</summary>
    public async Task<StateEvent> AddEventAsync(string? projectName, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(_userId) || string.IsNullOrWhiteSpace(_deviceId))
        {
            throw new InvalidOperationException("Connect before adding events.");
        }

        long sequence;
        lock (_stateLock)
        {
            _deviceSequence++;
            sequence = _deviceSequence;
        }

        bool isNone = string.IsNullOrWhiteSpace(projectName);
        StateEvent stateEvent = new(
            Guid.NewGuid(),
            _userId!,
            _deviceId!,
            sequence,
            DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
            isNone ? StateEventType.NoneSelected : StateEventType.ProjectSelected,
            isNone ? null : projectName!.Trim());

        await _gateway.UpdateAsync(stateEvent, cancellationToken);
        IngestEvent(stateEvent, isLocal: true);
        return stateEvent;
    }

    /// <summary>Edits an existing event (project name and/or timestamp). Requires online connection.</summary>
    public async Task EditEventAsync(Guid eventId, string? newProjectName, DateTime newOccurredAtUtc, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect before editing events.");
        }

        StateEvent existing;
        lock (_stateLock)
        {
            if (!_events.TryGetValue(eventId, out StateEvent? found))
            {
                throw new InvalidOperationException("Event not found.");
            }
            existing = found;
        }

        bool isNone = string.IsNullOrWhiteSpace(newProjectName);
        StateEvent updated = existing with
        {
            EventType = isNone ? StateEventType.NoneSelected : StateEventType.ProjectSelected,
            ProjectName = isNone ? null : newProjectName!.Trim(),
            OccurredAtUtc = DateTime.SpecifyKind(newOccurredAtUtc, DateTimeKind.Utc)
        };

        await _gateway.UpdateAsync(updated, cancellationToken);
        IngestEvent(updated, isLocal: true);
    }

    /// <summary>Deletes an existing event. Requires online connection.</summary>
    public async Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect before deleting events.");
        }

        await _gateway.DeleteAsync(eventId, cancellationToken);
        IngestRemove(eventId);
    }

    private void IngestEvent(StateEvent stateEvent, bool isLocal)
    {
        lock (_stateLock)
        {
            _events[stateEvent.EventId] = stateEvent;
            CurrentState = ComputeState();
        }

        StateChanged?.Invoke(CurrentState, stateEvent, isLocal);
        EventsChanged?.Invoke();
    }

    private void IngestRemove(Guid eventId)
    {
        StateEvent? removed;
        lock (_stateLock)
        {
            if (!_events.TryGetValue(eventId, out removed))
            {
                return;
            }
            _events.Remove(eventId);
            CurrentState = ComputeState();
        }

        StateChanged?.Invoke(CurrentState, removed, false);
        EventsChanged?.Invoke();
    }

    private TrackerState ComputeState()
    {
        TrackerState state = TrackerState.Empty;
        foreach (StateEvent evt in _events.Values
            .OrderBy(e => e.OccurredAtUtc)
            .ThenBy(e => e.EventId))
        {
            state = StateMachine.Apply(state, evt);
        }
        return state;
    }

    public void Dispose()
    {
        _gateway.Dispose();
        _syncWorker.Dispose();
    }
}
