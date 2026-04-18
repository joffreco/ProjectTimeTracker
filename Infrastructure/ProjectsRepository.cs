using Google.Cloud.Firestore;

namespace ProjectTimeTracker.Infrastructure;

public sealed class ProjectsRepository : IDisposable
{
    private readonly IGoogleFirestoreSession _session;
    private FirestoreChangeListener? _listener;
    private string? _userId;

    public ProjectsRepository(IGoogleFirestoreSession session)
    {
        _session = session;
    }

    public bool IsConnected => _session.IsConnected && !string.IsNullOrWhiteSpace(_userId);

    public void ConfigureUser(string userId)
    {
        _userId = userId;
    }

    public async Task<IReadOnlyList<string>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return [];
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);
        DocumentSnapshot snapshot = await doc.GetSnapshotAsync(cancellationToken);
        return MapItems(snapshot);
    }

    public async Task SaveAsync(IEnumerable<string> projects, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Projects repository is not connected.");
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);

        string[] items = projects
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await doc.SetAsync(new Dictionary<string, object?>
        {
            ["items"] = items,
            ["updatedAtUtc"] = Timestamp.FromDateTime(DateTime.UtcNow)
        }, SetOptions.Overwrite, cancellationToken);
    }

    public async Task StartListeningAsync(Action<IReadOnlyList<string>> onChanged, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect before listening to projects.");
        }

        await StopListeningAsync();

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);
        _listener = doc.Listen(snapshot => onChanged(MapItems(snapshot)));
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

    private static DocumentReference GetDocument(FirestoreDb db, string userId) =>
        db.Collection("timeTrackerUsers").Document(userId).Collection("state").Document("projects");

    private static IReadOnlyList<string> MapItems(DocumentSnapshot snapshot)
    {
        if (!snapshot.Exists)
        {
            return [];
        }

        if (!snapshot.TryGetValue("items", out List<string>? items) || items is null)
        {
            return [];
        }

        return items
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Dispose()
    {
        _ = StopListeningAsync();
    }
}

