namespace ProjectTimeTracker.Domain;

public enum StateEventType
{
    ProjectSelected,
    NoneSelected
}

public sealed record StateEvent(
    Guid EventId,
    string UserId,
    string DeviceId,
    long DeviceSequence,
    DateTime OccurredAtUtc,
    StateEventType EventType,
    string? ProjectName)
{
    public static StateEvent FromIntent(
        StateIntent intent,
        string userId,
        string deviceId,
        long deviceSequence,
        DateTime utcNow)
    {
        return intent.Type switch
        {
            StateIntentType.SelectProject => new StateEvent(
                Guid.NewGuid(),
                userId,
                deviceId,
                deviceSequence,
                utcNow,
                StateEventType.ProjectSelected,
                intent.ProjectName),
            StateIntentType.SelectNone => new StateEvent(
                Guid.NewGuid(),
                userId,
                deviceId,
                deviceSequence,
                utcNow,
                StateEventType.NoneSelected,
                null),
            _ => throw new ArgumentOutOfRangeException(nameof(intent.Type), intent.Type, "Unsupported intent type.")
        };
    }
}

