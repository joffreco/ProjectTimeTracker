namespace ProjectTimeTracker.Configuration;

/// <summary>
/// Strongly-typed settings bound from appsettings.json → "Firestore" section.
/// </summary>
public class FirestoreOptions
{
    public const string SectionName = "Firestore";

    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
    public string Collection { get; set; } = "events";
}

