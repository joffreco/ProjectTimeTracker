namespace ProjectTimeTracker.Domain;

/// <summary>
/// Holds Firebase Auth tokens returned after sign-in or token refresh.
/// </summary>
public class AuthToken
{
    public string IdToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the ID token has expired (with a 60-second safety margin).
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddSeconds(-60);
}

