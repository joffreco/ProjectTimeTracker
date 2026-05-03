using Google.Cloud.Firestore;

namespace ProjectTimeTracker.Infrastructure;

/// <summary>
/// Persists per-project, per-month invoice confirmations.
/// Keys are formatted as "{ProjectName}|YYYY-MM"; values are the UTC timestamp at which the invoice was confirmed.
/// </summary>
public sealed class InvoiceConfirmationsRepository : IDisposable
{
    private readonly IGoogleFirestoreSession _session;
    private FirestoreChangeListener? _listener;
    private string? _userId;

    public InvoiceConfirmationsRepository(IGoogleFirestoreSession session)
    {
        _session = session;
    }

    public bool IsConnected => _session.IsConnected && !string.IsNullOrWhiteSpace(_userId);

    public void ConfigureUser(string userId)
    {
        _userId = userId;
    }

    public static string MakeKey(string projectName, int year, int month) =>
        $"{projectName}|{year:D4}-{month:D2}";

    public async Task<IReadOnlyDictionary<string, DateTime>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);
        DocumentSnapshot snapshot = await doc.GetSnapshotAsync(cancellationToken);
        return MapEntries(snapshot);
    }

    public async Task ConfirmAsync(string projectName, int year, int month, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Invoice confirmations repository is not connected.");
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);
        string key = MakeKey(projectName, year, month);

        await doc.SetAsync(new Dictionary<string, object?>
        {
            ["entries"] = new Dictionary<string, object?>
            {
                [key] = Timestamp.FromDateTime(DateTime.UtcNow)
            },
            ["updatedAtUtc"] = Timestamp.FromDateTime(DateTime.UtcNow)
        }, SetOptions.MergeAll, cancellationToken);
    }

    public async Task UnconfirmAsync(string projectName, int year, int month, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Invoice confirmations repository is not connected.");
        }

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);
        string key = MakeKey(projectName, year, month);

        await doc.UpdateAsync(new Dictionary<FieldPath, object>
        {
            [new FieldPath("entries", key)] = FieldValue.Delete,
            [new FieldPath("updatedAtUtc")] = Timestamp.FromDateTime(DateTime.UtcNow)
        }, cancellationToken: cancellationToken);
    }

    public async Task StartListeningAsync(Action<IReadOnlyDictionary<string, DateTime>> onChanged, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect before listening to invoice confirmations.");
        }

        await StopListeningAsync();

        FirestoreDb db = await _session.CreateDbAsync(cancellationToken);
        DocumentReference doc = GetDocument(db, _userId!);
        _listener = doc.Listen(snapshot => onChanged(MapEntries(snapshot)));
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
        db.Collection("timeTrackerUsers").Document(userId).Collection("state").Document("invoiceConfirmations");

    private static IReadOnlyDictionary<string, DateTime> MapEntries(DocumentSnapshot snapshot)
    {
        Dictionary<string, DateTime> result = new(StringComparer.OrdinalIgnoreCase);
        if (!snapshot.Exists)
        {
            return result;
        }

        if (!snapshot.TryGetValue("entries", out IDictionary<string, object>? entries) || entries is null)
        {
            return result;
        }

        foreach (KeyValuePair<string, object> kv in entries)
        {
            DateTime utc = kv.Value switch
            {
                Timestamp ts => ts.ToDateTime(),
                DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                _ => DateTime.UtcNow
            };
            result[kv.Key] = utc;
        }

        return result;
    }

    public void Dispose()
    {
        _ = StopListeningAsync();
    }
}

