using Google.Cloud.Firestore;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Infrastructure;

public sealed class FirestoreEventGateway : IDisposable
{
    // New top-level collection. Each event document carries a `userId` field used to scope queries.
    private const string EventsCollectionName = "timeTrackerEvents";

    // Legacy location: timeTrackerUsers/{userId}/events. Still read once during connect for migration.
    private const string LegacyUsersCollectionName = "timeTrackerUsers";
    private const string LegacyEventsSubCollectionName = "events";

    private readonly IGoogleFirestoreSession _session;
    private FirestoreChangeListener? _listener;

    private string? _userId;

    public FirestoreEventGateway(IGoogleFirestoreSession session)
    {
        _session = session;
    }

    public bool IsConnected => _session.IsConnected && !string.IsNullOrWhiteSpace(_userId);

    public void ConfigureUser(string userId)
    {
        _userId = userId;
    }

    public async Task AppendAsync(StateEvent stateEvent, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Firestore gateway is not connected.");
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        CollectionReference eventsCollection = GetEventsCollection(db);

        DocumentReference doc = eventsCollection.Document(stateEvent.EventId.ToString("N"));
        await doc.SetAsync(BuildPayload(stateEvent), cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(StateEvent stateEvent, CancellationToken cancellationToken) =>
        AppendAsync(stateEvent, cancellationToken); // SetAsync overwrites the doc

    public async Task DeleteAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Firestore gateway is not connected.");
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        CollectionReference eventsCollection = GetEventsCollection(db);
        DocumentReference doc = eventsCollection.Document(eventId.ToString("N"));
        await doc.DeleteAsync(cancellationToken: cancellationToken);
    }

    private static Dictionary<string, object?> BuildPayload(StateEvent stateEvent) => new()
    {
        ["eventId"] = stateEvent.EventId.ToString("N"),
        ["userId"] = stateEvent.UserId,
        ["deviceId"] = stateEvent.DeviceId,
        ["deviceSequence"] = stateEvent.DeviceSequence,
        ["occurredAtUtc"] = Timestamp.FromDateTime(DateTime.SpecifyKind(stateEvent.OccurredAtUtc, DateTimeKind.Utc)),
        ["eventType"] = stateEvent.EventType.ToString(),
        ["projectName"] = stateEvent.ProjectName
    };

    public async Task<IReadOnlyList<StateEvent>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return [];
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        Query query = GetEventsCollection(db)
            .WhereEqualTo("userId", _userId!)
            .OrderBy("occurredAtUtc")
            .OrderBy("eventId")
            .Limit(1000);

        QuerySnapshot snapshot = await query.GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(MapDocument).ToArray();
    }

    public async Task StartListeningAsync(Action<StateEvent> onEvent, Action<Exception>? onError, CancellationToken cancellationToken) =>
        await StartListeningAsync(onEvent, onRemoved: null, onError, cancellationToken);

    public async Task StartListeningAsync(
        Action<StateEvent> onEvent,
        Action<Guid>? onRemoved,
        Action<Exception>? onError,
        CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect before starting listeners.");
        }

        await StopListeningAsync();

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        Query query = GetEventsCollection(db)
            .WhereEqualTo("userId", _userId!)
            .OrderBy("occurredAtUtc")
            .OrderBy("eventId")
            .Limit(1000);

        _listener = query.Listen(snapshot =>
        {
            foreach (DocumentChange change in snapshot.Changes)
            {
                if (change.ChangeType is DocumentChange.Type.Added or DocumentChange.Type.Modified)
                {
                    onEvent(MapDocument(change.Document));
                }
                else if (change.ChangeType == DocumentChange.Type.Removed && onRemoved is not null)
                {
                    if (Guid.TryParseExact(change.Document.Id, "N", out Guid removedId))
                    {
                        onRemoved(removedId);
                    }
                }
            }
        });
    }

    public async Task StopListeningAsync()
    {
        if (_listener is null)
        {
            return;
        }

        await _listener.StopAsync();
        _listener = null;
    }

    private static CollectionReference GetEventsCollection(FirestoreDb db) =>
        db.Collection(EventsCollectionName);

    private static CollectionReference GetLegacyEventsCollection(FirestoreDb db, string userId) =>
        db.Collection(LegacyUsersCollectionName).Document(userId).Collection(LegacyEventsSubCollectionName);

    /// <summary>
    /// Idempotently moves any events still living under the legacy
    /// <c>timeTrackerUsers/{userId}/events</c> sub-collection to the new top-level
    /// <c>timeTrackerEvents</c> collection. Safe to call repeatedly: documents already
    /// migrated are simply re-written (same content, same id) before the legacy copy is removed.
    /// </summary>
    /// <returns>The number of legacy documents migrated during this call.</returns>
    public async Task<int> MigrateLegacyEventsAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return 0;
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        CollectionReference legacy = GetLegacyEventsCollection(db, _userId!);
        CollectionReference target = GetEventsCollection(db);

        int migrated = 0;
        const int pageSize = 200;

        while (!cancellationToken.IsCancellationRequested)
        {
            QuerySnapshot page = await legacy.Limit(pageSize).GetSnapshotAsync(cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            WriteBatch batch = db.StartBatch();
            foreach (DocumentSnapshot legacyDoc in page.Documents)
            {
                Dictionary<string, object> data = legacyDoc.ToDictionary();

                // Defensive: legacy docs always belong to this user; ensure the field is set
                // so the new top-level collection can be filtered by userId.
                data["userId"] = _userId!;

                DocumentReference targetDoc = target.Document(legacyDoc.Id);
                batch.Set(targetDoc, data, SetOptions.Overwrite);
                batch.Delete(legacyDoc.Reference);
            }

            await batch.CommitAsync(cancellationToken);
            migrated += page.Count;

            // If the page wasn't full we've drained the legacy collection.
            if (page.Count < pageSize)
            {
                break;
            }
        }

        return migrated;
    }

    private static StateEvent MapDocument(DocumentSnapshot snapshot)
    {
        string eventId = snapshot.GetValue<string>("eventId");
        string userId = snapshot.GetValue<string>("userId");
        string deviceId = snapshot.GetValue<string>("deviceId");
        long deviceSequence = snapshot.GetValue<long>("deviceSequence");
        Timestamp ts = snapshot.GetValue<Timestamp>("occurredAtUtc");
        string eventType = snapshot.GetValue<string>("eventType");
        string? projectName = snapshot.TryGetValue("projectName", out string? value) ? value : null;

        return new StateEvent(
            Guid.ParseExact(eventId, "N"),
            userId,
            deviceId,
            deviceSequence,
            ts.ToDateTime().ToUniversalTime(),
            Enum.Parse<StateEventType>(eventType, ignoreCase: true),
            projectName);
    }

    public void Dispose()
    {
        _ = StopListeningAsync();
    }
}
