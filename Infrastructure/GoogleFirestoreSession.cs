using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Cloud.Firestore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectTimeTracker.Infrastructure;

public interface IGoogleFirestoreSession
{
    bool IsConnected { get; }

    string? ProjectId { get; }

    Task ConnectAsync(string secretPath, CancellationToken cancellationToken);

    Task<FirestoreDb> CreateDbAsync(CancellationToken cancellationToken);
}

public sealed class GoogleFirestoreSession : IGoogleFirestoreSession
{
    private static readonly string[] FirestoreScopes = ["https://www.googleapis.com/auth/datastore"];

    private UserCredential? _userCredential;

    public bool IsConnected => _userCredential is not null && !string.IsNullOrWhiteSpace(ProjectId);

    public string? ProjectId { get; private set; }

    public async Task ConnectAsync(string secretPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(secretPath))
        {
            throw new FileNotFoundException("Select your apps.googleusercontent.com JSON file first.", secretPath);
        }

        InstalledClientConfig installed = await ReadInstalledClientConfigAsync(secretPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(installed.ProjectId))
        {
            throw new InvalidOperationException("project_id is missing in the JSON file.");
        }

        _userCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets
            {
                ClientId = installed.ClientId,
                ClientSecret = installed.ClientSecret
            },
            FirestoreScopes,
            "desktop-user",
            cancellationToken,
            new FileDataStore(GetTokenStorePath(), true));

        ProjectId = installed.ProjectId;
    }

    public async Task<FirestoreDb> CreateDbAsync(CancellationToken cancellationToken)
    {
        if (_userCredential is null || string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new InvalidOperationException("Google authentication has not been initialized.");
        }

        await _userCredential.RefreshTokenAsync(cancellationToken);
        string? accessToken = _userCredential.Token.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("No access token returned by Google OAuth.");
        }

        GoogleCredential credential = GoogleCredential.FromAccessToken(accessToken);
        return new FirestoreDbBuilder
        {
            ProjectId = ProjectId,
            Credential = credential
        }.Build();
    }

    private static async Task<InstalledClientConfig> ReadInstalledClientConfigAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream fs = File.OpenRead(path);
        OAuthDesktopSecretFile? json = await JsonSerializer.DeserializeAsync<OAuthDesktopSecretFile>(fs, cancellationToken: cancellationToken);

        if (json?.Installed is null ||
            string.IsNullOrWhiteSpace(json.Installed.ClientId) ||
            string.IsNullOrWhiteSpace(json.Installed.ClientSecret))
        {
            throw new InvalidOperationException("Invalid desktop OAuth JSON. Expected an 'installed' section with client_id and client_secret.");
        }

        return json.Installed;
    }

    private static string GetTokenStorePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectTimeTracker.Auth");

    private sealed class OAuthDesktopSecretFile
    {
        [JsonPropertyName("installed")]
        public InstalledClientConfig? Installed { get; set; }
    }

    private sealed class InstalledClientConfig
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; } = string.Empty;

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = string.Empty;
    }
}

