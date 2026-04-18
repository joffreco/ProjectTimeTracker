using System.Text.Json;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Infrastructure;

public sealed class FileEventQueue : IEventQueue
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
    private readonly SemaphoreSlim _sync = new(1, 1);

    public FileEventQueue(string filePath)
    {
        _filePath = filePath;
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "[]");
        }
    }

    public async Task EnqueueAsync(StateEvent stateEvent, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            List<QueueItemDto> items = await ReadItemsUnsafeAsync(cancellationToken);
            long nextId = items.Count == 0 ? 1 : items.Max(i => i.LocalId) + 1;
            items.Add(new QueueItemDto
            {
                LocalId = nextId,
                Event = stateEvent,
                AttemptCount = 0,
                NextAttemptUtc = DateTime.UtcNow,
                Dispatched = false,
                LastError = null
            });

            await WriteItemsUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<IReadOnlyList<QueuedStateEvent>> GetDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            List<QueueItemDto> items = await ReadItemsUnsafeAsync(cancellationToken);
            return items
                .Where(i => !i.Dispatched && i.NextAttemptUtc <= utcNow)
                .OrderBy(i => i.NextAttemptUtc)
                .ThenBy(i => i.LocalId)
                .Take(maxCount)
                .Select(i => new QueuedStateEvent(i.LocalId, i.Event, i.AttemptCount, i.NextAttemptUtc, i.LastError))
                .ToArray();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task MarkDispatchedAsync(long localId, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            List<QueueItemDto> items = await ReadItemsUnsafeAsync(cancellationToken);
            QueueItemDto? match = items.FirstOrDefault(i => i.LocalId == localId);
            if (match is null)
            {
                return;
            }

            match.Dispatched = true;
            match.LastError = null;
            await WriteItemsUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task MarkFailedAsync(long localId, int attemptCount, DateTime nextAttemptUtc, string error, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            List<QueueItemDto> items = await ReadItemsUnsafeAsync(cancellationToken);
            QueueItemDto? match = items.FirstOrDefault(i => i.LocalId == localId);
            if (match is null)
            {
                return;
            }

            match.AttemptCount = attemptCount;
            match.NextAttemptUtc = nextAttemptUtc;
            match.LastError = error;
            await WriteItemsUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<List<QueueItemDto>> ReadItemsUnsafeAsync(CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        List<QueueItemDto>? data = await JsonSerializer.DeserializeAsync<List<QueueItemDto>>(stream, _jsonOptions, cancellationToken);
        return data ?? [];
    }

    private async Task WriteItemsUnsafeAsync(List<QueueItemDto> items, CancellationToken cancellationToken)
    {
        string tempPath = _filePath + ".tmp";
        await using (FileStream stream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, items, _jsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Copy(tempPath, _filePath, overwrite: true);
        File.Delete(tempPath);
    }

    private sealed class QueueItemDto
    {
        public long LocalId { get; set; }

        public StateEvent Event { get; set; } = null!;

        public int AttemptCount { get; set; }

        public DateTime NextAttemptUtc { get; set; }

        public bool Dispatched { get; set; }

        public string? LastError { get; set; }
    }
}

