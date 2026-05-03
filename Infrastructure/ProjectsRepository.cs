using Google.Cloud.Firestore;
using ProjectTimeTracker.Domain;

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

    public async Task<IReadOnlyList<ProjectDefinition>> ReadAsync(CancellationToken cancellationToken)
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

    public async Task SaveAsync(IEnumerable<ProjectDefinition> projects, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Projects repository is not connected.");
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);

        Dictionary<string, object?>[] items = projects
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new ProjectDefinition(p.Name.Trim(), p.IsInvoiceable))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new Dictionary<string, object?>
            {
                ["name"] = p.Name,
                ["invoiceable"] = p.IsInvoiceable
            })
            .ToArray();

        await doc.SetAsync(new Dictionary<string, object?>
        {
            ["items"] = items,
            ["updatedAtUtc"] = Timestamp.FromDateTime(DateTime.UtcNow)
        }, SetOptions.Overwrite, cancellationToken);
    }

    public async Task StartListeningAsync(Action<IReadOnlyList<ProjectDefinition>> onChanged, CancellationToken cancellationToken)
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

    private static IReadOnlyList<ProjectDefinition> MapItems(DocumentSnapshot snapshot)
    {
        if (!snapshot.Exists)
        {
            return [];
        }

        if (!snapshot.TryGetValue("items", out List<object>? raw) || raw is null)
        {
            return [];
        }

        List<ProjectDefinition> result = new();
        foreach (object item in raw)
        {
            switch (item)
            {
                case string s when !string.IsNullOrWhiteSpace(s):
                    // Legacy format: plain project name → not invoiceable.
                    result.Add(new ProjectDefinition(s.Trim(), false));
                    break;
                case IDictionary<string, object> map:
                    if (map.TryGetValue("name", out object? nameObj) && nameObj is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        bool invoiceable = false;
                        if (map.TryGetValue("invoiceable", out object? invObj))
                        {
                            invoiceable = invObj switch
                            {
                                bool b => b,
                                string sb => bool.TryParse(sb, out bool parsed) && parsed,
                                _ => false
                            };
                        }
                        result.Add(new ProjectDefinition(name.Trim(), invoiceable));
                    }
                    break;
            }
        }

        return result
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Dispose()
    {
        _ = StopListeningAsync();
    }
}
