using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectTimeTracker.Configuration;
using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Services;

/// <summary>
/// Firebase Authentication via REST API (identitytoolkit.googleapis.com).
/// Handles sign-in with email/password and automatic token refresh.
/// </summary>
public sealed class FirebaseAuthService : IAuthService, IDisposable
{
    private const string SignInUrl =
        "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={0}";

    private const string RefreshUrl =
        "https://securetoken.googleapis.com/v1/token?key={0}";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<FirebaseAuthService> _logger;
    private readonly System.Threading.Timer _refreshTimer;

    private AuthToken? _currentToken;
    private readonly object _lock = new();

    public FirebaseAuthService(
        HttpClient httpClient,
        IOptions<FirebaseAuthOptions> options,
        ILogger<FirebaseAuthService> logger)
    {
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
        _logger = logger;
        _refreshTimer = new System.Threading.Timer(OnRefreshTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    // ── IAuthService ────────────────────────────────────────────────────

    public bool IsAuthenticated
    {
        get
        {
            lock (_lock)
            {
                return _currentToken != null && !_currentToken.IsExpired;
            }
        }
    }

    public string? GetIdToken()
    {
        lock (_lock)
        {
            if (_currentToken != null && !_currentToken.IsExpired)
            {
                return _currentToken.IdToken;
            }
            return null;
        }
    }

    public async Task<AuthToken> SignInAsync(string email, string password)
    {
        _logger.LogInformation("Signing in with Firebase Auth for {Email}", email);

        string url = string.Format(SignInUrl, _apiKey);

        SignInRequest requestBody = new SignInRequest
        {
            Email = email,
            Password = password,
            ReturnSecureToken = true
        };

        string json = JsonSerializer.Serialize(requestBody);
        StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient.PostAsync(url, content);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Firebase Auth sign-in failed: {StatusCode} — {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"Firebase Auth sign-in failed ({response.StatusCode}): {responseBody}");
        }

        SignInResponse? result = JsonSerializer.Deserialize<SignInResponse>(responseBody);

        if (result == null || string.IsNullOrEmpty(result.IdToken))
        {
            throw new InvalidOperationException("Firebase Auth returned an empty response.");
        }

        int expiresIn = int.TryParse(result.ExpiresIn, out int parsed) ? parsed : 3600;

        AuthToken token = new AuthToken
        {
            IdToken = result.IdToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = expiresIn,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
        };

        lock (_lock)
        {
            _currentToken = token;
        }

        _logger.LogInformation("Firebase Auth sign-in successful — token expires at {ExpiresAt:u}", token.ExpiresAt);
        return token;
    }

    public async Task<AuthToken> RefreshTokenAsync(string refreshToken)
    {
        _logger.LogInformation("Refreshing Firebase Auth token");

        string url = string.Format(RefreshUrl, _apiKey);

        Dictionary<string, string> formData = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        FormUrlEncodedContent content = new FormUrlEncodedContent(formData);

        HttpResponseMessage response = await _httpClient.PostAsync(url, content);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Firebase Auth token refresh failed: {StatusCode} — {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"Firebase Auth token refresh failed ({response.StatusCode}): {responseBody}");
        }

        RefreshResponse? result = JsonSerializer.Deserialize<RefreshResponse>(responseBody);

        if (result == null || string.IsNullOrEmpty(result.IdToken))
        {
            throw new InvalidOperationException("Firebase Auth refresh returned an empty response.");
        }

        int expiresIn = int.TryParse(result.ExpiresIn, out int parsed) ? parsed : 3600;

        AuthToken token = new AuthToken
        {
            IdToken = result.IdToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = expiresIn,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
        };

        lock (_lock)
        {
            _currentToken = token;
        }

        _logger.LogInformation("Firebase Auth token refreshed — new expiry {ExpiresAt:u}", token.ExpiresAt);
        return token;
    }

    public void StartAutoRefresh()
    {
        // Refresh 5 minutes before expiry (55 min for a 60 min token).
        TimeSpan interval = TimeSpan.FromMinutes(55);
        _refreshTimer.Change(interval, interval);
        _logger.LogInformation("Auto-refresh timer started — interval {Interval}", interval);
    }

    public void StopAutoRefresh()
    {
        _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Auto-refresh timer stopped");
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
    }

    // ── Private ─────────────────────────────────────────────────────────

    private async void OnRefreshTimerElapsed(object? state)
    {
        try
        {
            string? refreshToken;
            lock (_lock)
            {
                refreshToken = _currentToken?.RefreshToken;
            }

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await RefreshTokenAsync(refreshToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-refresh of Firebase Auth token failed");
        }
    }

    // ── REST API DTOs (private) ─────────────────────────────────────────

    private sealed class SignInRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("returnSecureToken")]
        public bool ReturnSecureToken { get; set; }
    }

    private sealed class SignInResponse
    {
        [JsonPropertyName("idToken")]
        public string IdToken { get; set; } = string.Empty;

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expiresIn")]
        public string ExpiresIn { get; set; } = "3600";

        [JsonPropertyName("localId")]
        public string LocalId { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class RefreshResponse
    {
        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public string ExpiresIn { get; set; } = "3600";

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}

