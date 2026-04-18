namespace ProjectTimeTracker.Domain;

public enum StateIntentType
{
    SelectProject,
    SelectNone
}

public sealed record StateIntent(StateIntentType Type, string? ProjectName)
{
    public static StateIntent ForProject(string projectName) => new(StateIntentType.SelectProject, projectName);

    public static StateIntent None() => new(StateIntentType.SelectNone, null);
}

