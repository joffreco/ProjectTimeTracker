using Google.Cloud.Firestore;

namespace ProjectTimeTracker.Domain;

/// <summary>
/// Immutable event representing a state transition. Persisted to Firestore.
/// </summary>
[FirestoreData]
public class StateEvent
{
    [FirestoreProperty("fromState")]
    public string FromState { get; set; } = string.Empty;

    [FirestoreProperty("toState")]
    public string ToState { get; set; } = string.Empty;

    [FirestoreProperty("timestamp")]
    public Timestamp Timestamp { get; set; }

    /// <summary>
    /// Firestore document ID — not persisted as a field, set after read.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Creates a new StateEvent for a transition happening now (UTC).
    /// </summary>
    public static StateEvent Create(State from, State to)
    {
        return new StateEvent
        {
            FromState = from.ToString(),
            ToState = to.ToString(),
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
        };
    }

    /// <summary>
    /// Parses the ToState string back to a State enum.
    /// </summary>
    public State GetToState()
    {
        return Enum.TryParse<State>(ToState, ignoreCase: true, out State result)
            ? result
            : State.None;
    }

    /// <summary>
    /// Parses the FromState string back to a State enum.
    /// </summary>
    public State GetFromState()
    {
        return Enum.TryParse<State>(FromState, ignoreCase: true, out State result)
            ? result
            : State.None;
    }
}

