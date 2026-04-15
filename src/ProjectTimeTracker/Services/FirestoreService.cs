using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectTimeTracker.Configuration;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Services;

/// <summary>
/// Centralised Firestore SDK wrapper. All Firestore I/O passes through this class.
/// </summary>
public sealed class FirestoreService : IFirestoreService
{
    private readonly FirestoreDb _db;
    private readonly string _collection;
    private readonly ILogger<FirestoreService> _logger;

    public FirestoreService(FirestoreDb db, IOptions<FirestoreOptions> options, ILogger<FirestoreService> logger)
    {
        _db = db;
        _collection = options.Value.Collection;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentReference> SendEventAsync(StateEvent evt)
    {
        _logger.LogInformation(
            "Writing event {FromState} → {ToState} at {Timestamp}",
            evt.FromState, evt.ToState, evt.Timestamp);

        DocumentReference docRef = await _db.Collection(_collection).AddAsync(evt);

        _logger.LogInformation("Event written — DocumentId: {DocumentId}", docRef.Id);
        return docRef;
    }

    /// <inheritdoc />
    public async Task<List<StateEvent>> GetRecentEventsAsync(int limit = 50)
    {
        _logger.LogInformation("Fetching last {Limit} events from Firestore", limit);

        QuerySnapshot snapshot = await _db.Collection(_collection)
            .OrderByDescending("timestamp")
            .Limit(limit)
            .GetSnapshotAsync();

        List<StateEvent> events = new List<StateEvent>();

        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            StateEvent evt = doc.ConvertTo<StateEvent>();
            evt.DocumentId = doc.Id;
            events.Add(evt);
        }

        _logger.LogInformation("Loaded {Count} events from Firestore", events.Count);
        return events;
    }

    /// <inheritdoc />
    public FirestoreChangeListener StartListening(Action<QuerySnapshot> onSnapshot)
    {
        _logger.LogInformation("Starting Firestore Realtime Listener on '{Collection}'", _collection);

        Query query = _db.Collection(_collection).OrderByDescending("timestamp");

        FirestoreChangeListener listener = query.Listen(snapshot =>
        {
            _logger.LogInformation(
                "Listener snapshot received — {ChangeCount} changes",
                snapshot.Changes.Count);
            onSnapshot(snapshot);
        });

        return listener;
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectivityAsync()
    {
        try
        {
            // A lightweight read to verify connectivity.
            await _db.Collection(_collection).Limit(1).GetSnapshotAsync();
            _logger.LogInformation("Firestore connectivity OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firestore connectivity check failed");
            return false;
        }
    }
}

