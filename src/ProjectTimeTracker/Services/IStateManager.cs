using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Services;

/// <summary>
/// Manages in-memory current state, validates transitions,
/// and orchestrates persistence + realtime sync.
/// </summary>
public interface IStateManager
{
    /// <summary>Current tracking state held in memory.</summary>
    State CurrentState { get; }

    /// <summary>Raised whenever the state changes (local or remote).</summary>
    event Action<State>? StateChanged;

    /// <summary>
    /// Load recent events from Firestore, rebuild current state, start listener.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Request a state transition. Validates, creates event, writes to Firestore.
    /// </summary>
    Task ChangeStateAsync(State newState);

    /// <summary>
    /// Gracefully stop the realtime listener.
    /// </summary>
    Task StopAsync();
}

