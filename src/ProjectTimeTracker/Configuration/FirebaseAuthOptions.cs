namespace ProjectTimeTracker.Configuration;

/// <summary>
/// Strongly-typed settings bound from appsettings.json → "Firebase" section.
/// </summary>
public class FirebaseAuthOptions
{
    public const string SectionName = "Firebase";

    public string ApiKey { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

