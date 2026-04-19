using Google.Cloud.Firestore;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Infrastructure;

public sealed class FirestoreEventGateway : IDisposable
{
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
        CollectionReference eventsCollection = GetEventsCollection(db, _userId!);

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
        CollectionReference eventsCollection = GetEventsCollection(db, _userId!);
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
        Query query = GetEventsCollection(db, _userId!)
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
        Query query = GetEventsCollection(db, _userId!)
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

    private static CollectionReference GetEventsCollection(FirestoreDb db, string userId) =>
        db.Collection("timeTrackerUsers").Document(userId).Collection("events");

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
