namespace ProjectTimeTracker.Domain;

public static class StateMachine
{
    public static TrackerState Apply(TrackerState current, StateEvent stateEvent)
    {
        return stateEvent.EventType switch
        {
            StateEventType.ProjectSelected => new TrackerState(stateEvent.ProjectName, stateEvent.OccurredAtUtc),
            StateEventType.NoneSelected => new TrackerState(null, stateEvent.OccurredAtUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(stateEvent.EventType), stateEvent.EventType, "Unknown event type.")
        };
    }
}

