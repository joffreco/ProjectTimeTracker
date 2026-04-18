using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Cloud.Firestore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectTimeTracker;

public partial class Form1 : Form
{
    private static readonly string[] FirestoreScopes = ["https://www.googleapis.com/auth/datastore"];

    private UserCredential? _userCredential;
    private string? _projectId;
    private FirestoreDb? _db;

    public Form1()
    {
        InitializeComponent();
        TryPrefillSecretPath();
    }

    private void btnBrowse_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Google Desktop Client Secret (*.json)|*.json|All files (*.*)|*.*",
            Title = "Select apps.googleusercontent.com JSON"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtSecretPath.Text = dialog.FileName;
        }
    }

    private async void btnConnect_Click(object sender, EventArgs e)
    {
        try
        {
            SetBusyState(true, "Opening Google sign-in...");
            await InitializeFirestoreAsync();
            await LoadDocumentsAsync();
            SetBusyState(false, $"Connected to project '{_projectId}'.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Connection failed: {ex.Message}");
        }
    }

    private async void btnLoadDocs_Click(object sender, EventArgs e)
    {
        try
        {
            SetBusyState(true, "Loading documents...");
            await LoadDocumentsAsync();
            SetBusyState(false, "Documents loaded.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Load failed: {ex.Message}");
        }
    }

    private async void btnAddSample_Click(object sender, EventArgs e)
    {
        try
        {
            if (_db is null)
            {
                throw new InvalidOperationException("Connect first.");
            }

            SetBusyState(true, "Adding sample document...");
            await EnsureValidDbAsync();

            string collectionName = GetCollectionName();
            string id = Guid.NewGuid().ToString("N")[..8];
            DocumentReference doc = _db!.Collection(collectionName).Document(id);
            await doc.SetAsync(new
            {
                createdAtUtc = DateTime.UtcNow,
                note = "Created from FirestoreDesktopMini"
            });

            await LoadDocumentsAsync();
            SetBusyState(false, $"Added document: {id}");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Add failed: {ex.Message}");
        }
    }

    private async Task InitializeFirestoreAsync()
    {
        if (!File.Exists(txtSecretPath.Text))
        {
            throw new FileNotFoundException("Select your apps.googleusercontent.com JSON file first.");
        }

        InstalledClientConfig installed = await ReadInstalledClientConfigAsync(txtSecretPath.Text);
        _projectId = installed.ProjectId;

        if (string.IsNullOrWhiteSpace(_projectId))
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
            CancellationToken.None,
            new FileDataStore(GetTokenStorePath(), true));

        await EnsureValidDbAsync();
    }

    private async Task EnsureValidDbAsync()
    {
        if (_userCredential is null || string.IsNullOrWhiteSpace(_projectId))
        {
            throw new InvalidOperationException("Google authentication has not been initialized.");
        }

        await _userCredential.RefreshTokenAsync(CancellationToken.None);

        string? accessToken = _userCredential.Token.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("No access token returned by Google OAuth.");
        }

        GoogleCredential credential = GoogleCredential.FromAccessToken(accessToken);
        _db = new FirestoreDbBuilder
        {
            ProjectId = _projectId,
            Credential = credential
        }.Build();
    }

    private async Task LoadDocumentsAsync()
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Connect first.");
        }

        await EnsureValidDbAsync();

        string collectionName = GetCollectionName();
        QuerySnapshot snapshot = await _db!.Collection(collectionName).Limit(20).GetSnapshotAsync();

        lstDocs.Items.Clear();
        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            lstDocs.Items.Add(doc.Id);
        }

        if (snapshot.Count == 0)
        {
            lstDocs.Items.Add("(no documents yet)");
        }
    }

    private string GetCollectionName()
    {
        string value = txtCollection.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Collection name cannot be empty.");
        }

        return value;
    }

    private static async Task<InstalledClientConfig> ReadInstalledClientConfigAsync(string path)
    {
        await using FileStream fs = File.OpenRead(path);
        OAuthDesktopSecretFile? json = await JsonSerializer.DeserializeAsync<OAuthDesktopSecretFile>(fs);

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

    private void TryPrefillSecretPath()
    {
        string downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloadPath = Path.Combine(downloads, "Downloads");

        if (!Directory.Exists(downloadPath))
        {
            return;
        }

        string[] matches = Directory.GetFiles(downloadPath, "client_secret_*.apps.googleusercontent.com.json");
        if (matches.Length > 0)
        {
            txtSecretPath.Text = matches[0];
        }
    }

    private void SetBusyState(bool busy, string message)
    {
        btnConnect.Enabled = !busy;
        btnLoadDocs.Enabled = !busy;
        btnAddSample.Enabled = !busy;
        lblStatus.Text = message;
    }

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
