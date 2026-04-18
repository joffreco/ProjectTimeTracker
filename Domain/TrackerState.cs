namespace ProjectTimeTracker.Domain;

public sealed record TrackerState(string? CurrentProject, DateTime? SinceUtc)
{
    public static TrackerState Empty { get; } = new(null, null);

    public bool IsNone => string.IsNullOrWhiteSpace(CurrentProject);
}

