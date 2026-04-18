namespace ProjectTimeTracker.Domain;

public static class TransitionValidator
{
    public static void Validate(TrackerState state, StateIntent intent)
    {
        if (intent.Type == StateIntentType.SelectProject)
        {
            if (string.IsNullOrWhiteSpace(intent.ProjectName))
            {
                throw new InvalidOperationException("Project name cannot be empty.");
            }

            if (string.Equals(state.CurrentProject, intent.ProjectName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This project is already active.");
            }

            return;
        }

        if (intent.Type == StateIntentType.SelectNone && state.IsNone)
        {
            throw new InvalidOperationException("State is already none.");
        }
    }
}

