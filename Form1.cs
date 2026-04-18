using ProjectTimeTracker.Application;
using ProjectTimeTracker.Domain;
using System.Text.Json;

namespace ProjectTimeTracker;

public partial class Form1 : Form
{
    private readonly TrackerAppService _appService;
    private readonly List<string> _projects = [];
    private readonly Dictionary<string, Button> _projectButtons = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeProject;

    private string ProjectsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProjectTimeTracker",
        "projects.json");

    public Form1(TrackerAppService appService)
    {
        _appService = appService;
        InitializeComponent();
        ConfigureTrackerUi();
        TryPrefillSecretPath();
        LoadProjects();
        RebuildProjectsUi();
        _appService.StateChanged += AppServiceOnStateChanged;
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
            await _appService.ConnectAsync(txtSecretPath.Text.Trim(), "joff", CancellationToken.None);
            SetBusyState(false, "Connected. Listening for events...");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Connection failed: {ex.Message}");
        }
    }

    private async void btnNone_Click(object sender, EventArgs e)
    {
        try
        {
            SetBusyState(true, "Switching to none...");
            await _appService.EmitIntentAsync(StateIntent.None(), CancellationToken.None);
            SetBusyState(false, "State changed to none.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Switch failed: {ex.Message}");
        }
    }

    private void btnAddProject_Click(object sender, EventArgs e)
    {
        try
        {
            string name = txtProjectName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Project name cannot be empty.");
            }

            if (_projects.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Project already exists.");
            }

            _projects.Add(name);
            _projects.Sort(StringComparer.OrdinalIgnoreCase);
            SaveProjects();
            RebuildProjectsUi();
            txtProjectName.Clear();
            SetBusyState(false, $"Project '{name}' added.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Add failed: {ex.Message}");
        }
    }

    private async void btnDeleteProject_Click(object sender, EventArgs e)
    {
        try
        {
            string? selected = lstProjects.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selected))
            {
                throw new InvalidOperationException("Select a project to delete.");
            }

            if (string.Equals(_activeProject, selected, StringComparison.OrdinalIgnoreCase))
            {
                SetBusyState(true, "Project is active, switching to none first...");
                await _appService.EmitIntentAsync(StateIntent.None(), CancellationToken.None);
            }

            _projects.RemoveAll(p => string.Equals(p, selected, StringComparison.OrdinalIgnoreCase));
            SaveProjects();
            RebuildProjectsUi();
            SetBusyState(false, $"Project '{selected}' deleted.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Delete failed: {ex.Message}");
        }
    }

    private async void ProjectButtonOnClick(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not string projectName)
            {
                return;
            }

            SetBusyState(true, $"Switching project to {projectName}...");
            await _appService.EmitIntentAsync(StateIntent.ForProject(projectName), CancellationToken.None);
            SetBusyState(false, $"Active project: {projectName}");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Switch failed: {ex.Message}");
        }
    }

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

    private void ConfigureTrackerUi()
    {
        btnAddProject.Text = "Add";
        btnDeleteProject.Text = "Delete";
        btnNone.Text = "None";
        lblStatus.Text = "Connect, then pick a project.";
        Text = "ProjectTimeTracker";
    }

    private void AppServiceOnStateChanged(TrackerState state, StateEvent stateEvent, bool isLocal)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppServiceOnStateChanged(state, stateEvent, isLocal));
            return;
        }

        string source = isLocal ? "local" : "remote";
        string project = state.IsNone ? "none" : state.CurrentProject!;
        _activeProject = state.IsNone ? null : state.CurrentProject;
        UpdateActiveButtonStyles();
        lblStatus.Text = $"Current: {project} (since {state.SinceUtc:HH:mm:ss})";
        lstDocs.Items.Insert(0,
            $"{stateEvent.OccurredAtUtc:HH:mm:ss} [{source}] {stateEvent.EventType} {(stateEvent.ProjectName ?? "none")}");

        while (lstDocs.Items.Count > 200)
        {
            lstDocs.Items.RemoveAt(lstDocs.Items.Count - 1);
        }
    }

    private void SetBusyState(bool busy, string message)
    {
        btnConnect.Enabled = !busy;
        btnNone.Enabled = !busy;
        btnAddProject.Enabled = !busy;
        btnDeleteProject.Enabled = !busy;
        txtProjectName.Enabled = !busy;
        lstProjects.Enabled = !busy;
        flpProjectButtons.Enabled = !busy;
        lblStatus.Text = message;
    }

    private void RebuildProjectsUi()
    {
        lstProjects.BeginUpdate();
        try
        {
            lstProjects.Items.Clear();
            foreach (string project in _projects)
            {
                lstProjects.Items.Add(project);
            }
        }
        finally
        {
            lstProjects.EndUpdate();
        }

        flpProjectButtons.SuspendLayout();
        try
        {
            flpProjectButtons.Controls.Clear();
            _projectButtons.Clear();

            foreach (string project in _projects)
            {
                Button projectButton = new()
                {
                    AutoSize = true,
                    Margin = new Padding(4),
                    Tag = project,
                    Text = project
                };
                projectButton.Click += ProjectButtonOnClick;
                _projectButtons[project] = projectButton;
                flpProjectButtons.Controls.Add(projectButton);
            }
        }
        finally
        {
            flpProjectButtons.ResumeLayout();
        }

        UpdateActiveButtonStyles();
    }

    private void UpdateActiveButtonStyles()
    {
        foreach ((string name, Button button) in _projectButtons)
        {
            bool isActive = !string.IsNullOrWhiteSpace(_activeProject) &&
                            string.Equals(name, _activeProject, StringComparison.OrdinalIgnoreCase);
            button.BackColor = isActive ? Color.LightGreen : SystemColors.Control;
            button.Font = new Font(button.Font, isActive ? FontStyle.Bold : FontStyle.Regular);
        }

        bool noneActive = string.IsNullOrWhiteSpace(_activeProject);
        btnNone.BackColor = noneActive ? Color.LightGreen : SystemColors.Control;
        btnNone.Font = new Font(btnNone.Font, noneActive ? FontStyle.Bold : FontStyle.Regular);
    }

    private void LoadProjects()
    {
        if (!File.Exists(ProjectsFilePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(ProjectsFilePath);
            string[]? loaded = JsonSerializer.Deserialize<string[]>(json);
            if (loaded is null)
            {
                return;
            }

            _projects.Clear();
            _projects.AddRange(loaded.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
            _projects.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Keep UI usable even if local project file is malformed.
        }
    }

    private void SaveProjects()
    {
        string directory = Path.GetDirectoryName(ProjectsFilePath)!;
        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(_projects);
        File.WriteAllText(ProjectsFilePath, json);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _appService.StateChanged -= AppServiceOnStateChanged;
        _appService.Dispose();
        base.OnFormClosed(e);
    }
}
