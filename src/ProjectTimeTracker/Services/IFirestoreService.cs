using Google.Cloud.Firestore;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Services;

/// <summary>
/// Abstraction over Firestore SDK — all Firestore I/O goes through here.
/// </summary>
public interface IFirestoreService
{
    /// <summary>
    /// Append a single immutable event to Firestore.
    /// </summary>
    Task<DocumentReference> SendEventAsync(StateEvent evt);

    /// <summary>
    /// Fetch the most recent events (ordered by timestamp descending).
    /// </summary>
    Task<List<StateEvent>> GetRecentEventsAsync(int limit = 50);

    /// <summary>
    /// Start a Firestore Realtime Listener on the events collection.
    /// Returns a FirestoreChangeListener that can be stopped.
    /// </summary>
    FirestoreChangeListener StartListening(Action<QuerySnapshot> onSnapshot);

    /// <summary>
    /// Verify that Firestore is reachable.
    /// </summary>
    Task<bool> TestConnectivityAsync();
}

