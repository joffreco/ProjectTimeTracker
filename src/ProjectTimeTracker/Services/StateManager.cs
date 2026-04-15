using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Services;

/// <summary>
/// Maintains the in-memory current state, validates transitions,
/// and keeps all devices in sync through the Firestore Realtime Listener.
/// </summary>
public sealed class StateManager : IStateManager
{
    private readonly IFirestoreService _firestoreService;
    private readonly ILogger<StateManager> _logger;
    private readonly object _lock = new();

    private State _currentState = State.None;
    private FirestoreChangeListener? _listener;

    public StateManager(IFirestoreService firestoreService, ILogger<StateManager> logger)
    {
        _firestoreService = firestoreService;
        _logger = logger;
    }

    /// <inheritdoc />
    public State CurrentState
    {
        get { lock (_lock) { return _currentState; } }
        private set
        {
            lock (_lock)
            {
                if (_currentState == value) return;
                _currentState = value;
            }
            _logger.LogInformation("Current state changed to {State}", value);
            StateChanged?.Invoke(value);
        }
    }

    /// <inheritdoc />
    public event Action<State>? StateChanged;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing StateManager — loading recent events");

        try
        {
            // Rebuild current state from the most recent event.
            List<StateEvent> recentEvents = await _firestoreService.GetRecentEventsAsync(1);

            if (recentEvents.Count > 0)
            {
                StateEvent latest = recentEvents[0];
                CurrentState = latest.GetToState();
                _logger.LogInformation(
                    "State rebuilt from latest event: {FromState} → {ToState}",
                    latest.FromState, latest.ToState);
            }
            else
            {
                CurrentState = State.None;
                _logger.LogInformation("No events found — starting with State.None");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent events — starting with State.None");
            CurrentState = State.None;
        }

        // Start realtime listener for multi-device sync.
        _listener = _firestoreService.StartListening(OnSnapshotReceived);
    }

    /// <inheritdoc />
    public async Task ChangeStateAsync(State newState)
    {
        State current = CurrentState;

        // Ignore duplicate transitions.
        if (current == newState)
        {
            _logger.LogInformation("Ignoring duplicate transition to {State}", newState);
            return;
        }

        // Validate: switching between projects requires intermediate NONE.
        if (current != State.None && newState != State.None)
        {
            _logger.LogWarning(
                "Invalid transition {From} → {To} — must go through None first",
                current, newState);
            return;
        }

        StateEvent evt = StateEvent.Create(current, newState);
        _logger.LogInformation("Creating event: {From} → {To}", evt.FromState, evt.ToState);

        try
        {
            await _firestoreService.SendEventAsync(evt);
            // The listener will update CurrentState when Firestore confirms.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write event to Firestore");
            // Optimistic update so the local UI stays responsive.
            CurrentState = newState;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_listener != null)
        {
            _logger.LogInformation("Stopping Firestore Realtime Listener");
            await _listener.StopAsync();
            _listener = null;
        }
    }

    // ── Realtime Listener callback ──────────────────────────────────────

    private void OnSnapshotReceived(QuerySnapshot snapshot)
    {
        foreach (DocumentChange change in snapshot.Changes)
        {
            if (change.ChangeType == DocumentChange.Type.Added)
            {
                StateEvent evt = change.Document.ConvertTo<StateEvent>();
                evt.DocumentId = change.Document.Id;

                _logger.LogInformation(
                    "Listener ADDED — {From} → {To}  (doc {Id})",
                    evt.FromState, evt.ToState, evt.DocumentId);

                // Update in-memory state to the latest event's target.
                CurrentState = evt.GetToState();
            }
        }
    }
}

