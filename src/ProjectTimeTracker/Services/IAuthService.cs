using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Services;

/// <summary>
/// Abstraction over Firebase Authentication (Email/Password via REST API).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Sign in with email and password. Returns tokens on success.
    /// </summary>
    Task<AuthToken> SignInAsync(string email, string password);

    /// <summary>
    /// Use a refresh token to obtain a new ID token.
    /// </summary>
    Task<AuthToken> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Whether a valid (non-expired) ID token is currently held.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Return the current ID token, or null if not authenticated / expired.
    /// </summary>
    string? GetIdToken();

    /// <summary>
    /// Start a background timer that refreshes the token before expiry.
    /// </summary>
    void StartAutoRefresh();

    /// <summary>
    /// Stop the background refresh timer.
    /// </summary>
    void StopAutoRefresh();
}

