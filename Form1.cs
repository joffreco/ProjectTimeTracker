using ProjectTimeTracker.Application;
using ProjectTimeTracker.Domain;
using ProjectTimeTracker.Infrastructure;
using ProjectTimeTracker.UI;
using System.Text.Json;

namespace ProjectTimeTracker;

public partial class Form1 : Form
{
    private const string NavProjects = "Projects";
    private const string NavStatuses = "Statuses";

    private readonly TrackerAppService _appService;
    private readonly ProjectsRepository _projectsRepository;
    private readonly List<string> _projects = [];
    private readonly Dictionary<string, Button> _projectButtons = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeProject;

    private static readonly TimeZoneInfo MontrealTimeZone = ResolveMontrealTimeZone();

    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private ToolStripSeparator? _trayProjectsSeparator;
    private readonly List<ToolStripItem> _trayProjectItems = [];
    private ToolStripMenuItem? _trayAutostartItem;
    private bool _exitRequested;

    private readonly ProjectsView _projectsView = new() { Dock = DockStyle.Fill };
    private readonly StatusesView _statusesView = new() { Dock = DockStyle.Fill };

    private System.Windows.Forms.Timer? _reconnectTimer;
    private bool _isConnected;

    private string ProjectsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProjectTimeTracker",
        "projects.json");

    public Form1(TrackerAppService appService, ProjectsRepository projectsRepository)
    {
        _appService = appService;
        _projectsRepository = projectsRepository;
        InitializeComponent();
        ConfigureTrackerUi();
        InitializeNavigation();
        LoadProjects();
        RebuildProjectsUi();
        InitializeTrayIcon();
        TryEnableAutostartByDefault();

        if (HasTrayStartupArg())
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Load += (_, _) => Hide();
        }
        _appService.StateChanged += AppServiceOnStateChanged;
        _appService.EventsChanged += AppServiceOnEventsChanged;

        Load += (_, _) => BeginInvoke(new Action(() => _ = ConnectAsync()));
    }

    private void InitializeNavigation()
    {
        pnlContent.Controls.Add(_projectsView);
        pnlContent.Controls.Add(_statusesView);
        _projectsView.Visible = true;
        _statusesView.Visible = false;

        _projectsView.AddRequested += (_, _) => HandleAddProject();
        _projectsView.EditRequested += (_, name) => HandleEditProject(name);
        _projectsView.DeleteRequested += (_, name) => _ = HandleDeleteProjectAsync(name);

        _statusesView.AddRequested += (_, _) => _ = HandleAddStatusAsync();
        _statusesView.EditRequested += (_, eventId) => _ = HandleEditStatusAsync(eventId);
        _statusesView.DeleteRequested += (_, eventId) => _ = HandleDeleteStatusAsync(eventId);

        lstNav.Items.Add(NavProjects);
        lstNav.Items.Add(NavStatuses);
        lstNav.SelectedIndexChanged += (_, _) => SwitchView(lstNav.SelectedItem as string);
        lstNav.SelectedIndex = 0;
    }

    private void SwitchView(string? selection)
    {
        bool projects = string.Equals(selection, NavProjects, StringComparison.Ordinal);
        _projectsView.Visible = projects;
        _statusesView.Visible = !projects;
    }

    private void HandleAddProject()
    {
        try
        {
            string? name = InputDialog.Prompt(this, "Add project", "Project name:");
            if (name is null)
            {
                return;
            }

            if (_projects.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Project already exists.");
            }

            _projects.Add(name);
            _projects.Sort(StringComparer.OrdinalIgnoreCase);
            SaveProjects();
            RebuildProjectsUi();
            _ = TryPushProjectsAsync();
            SetBusyState(false, $"Project '{name}' added.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Add failed: {ex.Message}");
        }
    }

    private void HandleEditProject(string oldName)
    {
        try
        {
            string? newName = InputDialog.Prompt(this, "Rename project", "New name:", oldName);
            if (newName is null || string.Equals(newName, oldName, StringComparison.Ordinal))
            {
                return;
            }

            if (_projects.Any(p => string.Equals(p, newName, StringComparison.OrdinalIgnoreCase)
                                    && !string.Equals(p, oldName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Another project with this name already exists.");
            }

            int index = _projects.FindIndex(p => string.Equals(p, oldName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new InvalidOperationException("Project not found.");
            }

            _projects[index] = newName;
            _projects.Sort(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(_activeProject, oldName, StringComparison.OrdinalIgnoreCase))
            {
                _activeProject = newName;
            }

            SaveProjects();
            RebuildProjectsUi();
            _ = TryPushProjectsAsync();
            SetBusyState(false, $"Renamed '{oldName}' → '{newName}'.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Rename failed: {ex.Message}");
        }
    }

    private async Task HandleDeleteProjectAsync(string name)
    {
        try
        {
            DialogResult confirm = MessageBox.Show(this,
                $"Delete project '{name}'? Past events will keep referencing this name.",
                "Delete project",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            if (string.Equals(_activeProject, name, StringComparison.OrdinalIgnoreCase))
            {
                SetBusyState(true, "Project is active, switching to none first...");
                await _appService.EmitIntentAsync(StateIntent.None(), CancellationToken.None);
            }

            _projects.RemoveAll(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
            SaveProjects();
            RebuildProjectsUi();
            _ = TryPushProjectsAsync();
            SetBusyState(false, $"Project '{name}' deleted.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Delete failed: {ex.Message}");
        }
    }

    private async Task HandleAddStatusAsync()
    {
        try
        {
            DateTime initialLocal = ToMontreal(DateTime.UtcNow);
            using StatusEditDialog dialog = new(_projects, initialLocal, _activeProject);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            DateTime localTime = DateTime.SpecifyKind(dialog.SelectedLocalTime, DateTimeKind.Unspecified);
            DateTime utc = TimeZoneInfo.ConvertTimeToUtc(localTime, MontrealTimeZone);
            string? newProject = dialog.SelectedProject;

            SetBusyState(true, "Adding status...");
            StateEvent created = await _appService.AddEventAsync(newProject, utc, CancellationToken.None);
            await PruneRedundantNeighborsAsync(created.EventId, CancellationToken.None);
            SetBusyState(false, "Status added.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Add failed: {ex.Message}");
        }
    }

    private async Task HandleEditStatusAsync(Guid eventId)
    {
        try
        {
            if (!_appService.AllEvents.TryGetValue(eventId, out StateEvent? existing))
            {
                return;
            }

            DateTime initialLocal = ToMontreal(existing.OccurredAtUtc);
            using StatusEditDialog dialog = new(_projects, initialLocal, existing.ProjectName);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            DateTime localTime = DateTime.SpecifyKind(dialog.SelectedLocalTime, DateTimeKind.Unspecified);
            DateTime utc = TimeZoneInfo.ConvertTimeToUtc(localTime, MontrealTimeZone);
            string? newProject = dialog.SelectedProject;

            SetBusyState(true, "Updating status...");
            await _appService.EditEventAsync(eventId, newProject, utc, CancellationToken.None);
            await PruneRedundantNeighborsAsync(eventId, CancellationToken.None);
            SetBusyState(false, "Status updated.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Edit failed: {ex.Message}");
        }
    }

    private async Task PruneRedundantNeighborsAsync(Guid editedEventId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, StateEvent> all = _appService.AllEvents;
        if (!all.TryGetValue(editedEventId, out StateEvent? edited))
        {
            return;
        }

        List<StateEvent> sorted = all.Values
            .OrderBy(e => e.OccurredAtUtc)
            .ThenBy(e => e.EventId)
            .ToList();

        int idx = sorted.FindIndex(e => e.EventId == editedEventId);
        if (idx < 0)
        {
            return;
        }

        StateEvent? prev = idx > 0 ? sorted[idx - 1] : null;
        StateEvent? next = idx < sorted.Count - 1 ? sorted[idx + 1] : null;

        // Edited entry is redundant: previous already established the same project.
        if (prev is not null && SameProject(prev, edited))
        {
            await _appService.DeleteEventAsync(editedEventId, cancellationToken);
            return;
        }

        // Next entry is redundant: edited now establishes the same project the next one was switching to.
        if (next is not null && SameProject(edited, next))
        {
            await _appService.DeleteEventAsync(next.EventId, cancellationToken);
        }
    }

    private static bool SameProject(StateEvent a, StateEvent b)
    {
        bool aNone = a.EventType == StateEventType.NoneSelected;
        bool bNone = b.EventType == StateEventType.NoneSelected;
        if (aNone && bNone)
        {
            return true;
        }
        if (aNone != bNone)
        {
            return false;
        }
        return string.Equals(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleDeleteStatusAsync(Guid eventId)
    {
        try
        {
            DialogResult confirm = MessageBox.Show(this,
                "Delete this status entry? The historical state will be recomputed without it.",
                "Delete status",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            SetBusyState(true, "Deleting status...");
            await _appService.DeleteEventAsync(eventId, CancellationToken.None);
            SetBusyState(false, "Status deleted.");
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Delete failed: {ex.Message}");
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            SetBusyState(true, "Opening Google sign-in...");
            await _appService.ConnectAsync("joff", CancellationToken.None);

            _projectsRepository.ConfigureUser("joff");
            IReadOnlyList<string> remoteProjects = await _projectsRepository.ReadAsync(CancellationToken.None);
            if (remoteProjects.Count > 0)
            {
                ApplyRemoteProjects(remoteProjects);
            }
            else if (_projects.Count > 0)
            {
                await _projectsRepository.SaveAsync(_projects, CancellationToken.None);
            }

            await _projectsRepository.StartListeningAsync(items =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => ApplyRemoteProjects(items));
                }
                else
                {
                    ApplyRemoteProjects(items);
                }
            }, CancellationToken.None);

            SetBusyState(false, "Connected. Listening for events and projects...");
            _isConnected = true;
        }
        catch (Exception ex)
        {
            SetBusyState(false, $"Connection failed: {ex.Message}. Retrying in 10s...");
            ScheduleReconnect();
        }
    }

    private void ScheduleReconnect()
    {
        if (_isConnected)
        {
            return;
        }

        if (_reconnectTimer is null)
        {
            _reconnectTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
            _reconnectTimer.Tick += (_, _) =>
            {
                _reconnectTimer!.Stop();
                _ = ConnectAsync();
            };
        }

        _reconnectTimer.Stop();
        _reconnectTimer.Start();
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

    private void ConfigureTrackerUi()
    {
        btnNone.Text = "None";
        lblStatus.Text = "Connecting...";
        Text = "ProjectTimeTracker";
    }

    private void AppServiceOnStateChanged(TrackerState state, StateEvent stateEvent, bool isLocal)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppServiceOnStateChanged(state, stateEvent, isLocal));
            return;
        }

        string project = state.IsNone ? "none" : state.CurrentProject!;
        _activeProject = state.IsNone ? null : state.CurrentProject;
        UpdateActiveButtonStyles();
        if (_trayIcon is not null)
        {
            _trayIcon.Text = state.IsNone ? "ProjectTimeTracker - none" : $"ProjectTimeTracker - {state.CurrentProject}";
            UpdateTrayIconImage(state.IsNone ? null : state.CurrentProject);
        }
        string sinceText = state.SinceUtc.HasValue
            ? ToMontreal(state.SinceUtc.Value).ToString("HH:mm:ss")
            : "-";
        lblStatus.Text = $"Current: {project} (since {sinceText})";
    }

    private void AppServiceOnEventsChanged()
    {
        if (InvokeRequired)
        {
            BeginInvoke(AppServiceOnEventsChanged);
            return;
        }

        RefreshStatusesView();
    }

    private void RefreshStatusesView()
    {
        IReadOnlyDictionary<Guid, StateEvent> events = _appService.AllEvents;
        IEnumerable<StatusEntry> entries = events.Values.Select(e => new StatusEntry
        {
            EventId = e.EventId,
            LocalTime = ToMontreal(e.OccurredAtUtc),
            Source = "stored",
            EventType = e.EventType,
            Project = e.ProjectName ?? "none"
        });
        _statusesView.SetEntries(entries, ToMontreal(DateTime.UtcNow));
    }

    private void SetBusyState(bool busy, string message)
    {
        btnNone.Enabled = !busy;
        flpProjectButtons.Enabled = !busy;
        _projectsView.Enabled = !busy;
        _statusesView.Enabled = !busy;
        lblStatus.Text = message;
    }

    private void RebuildProjectsUi()
    {
        _projectsView.SetProjects(_projects);

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
        RebuildTrayProjectsMenu();
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

        UpdateTrayActiveStyles();
    }

    private void UpdateTrayActiveStyles()
    {
        foreach (ToolStripItem item in _trayProjectItems)
        {
            if (item is not ToolStripMenuItem menuItem || menuItem.Tag is not string projectName)
            {
                continue;
            }

            bool isActive = !string.IsNullOrWhiteSpace(_activeProject) &&
                            string.Equals(projectName, _activeProject, StringComparison.OrdinalIgnoreCase);
            menuItem.Checked = isActive;
            menuItem.BackColor = isActive ? Color.LightGreen : SystemColors.Control;
            menuItem.Font = new Font(menuItem.Font, isActive ? FontStyle.Bold : FontStyle.Regular);
        }
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

    private void ApplyRemoteProjects(IReadOnlyList<string> remote)
    {
        bool changed = remote.Count != _projects.Count
                       || !remote.SequenceEqual(_projects, StringComparer.OrdinalIgnoreCase);
        if (!changed)
        {
            return;
        }

        _projects.Clear();
        _projects.AddRange(remote);
        SaveProjects();
        RebuildProjectsUi();
    }

    private async Task TryPushProjectsAsync()
    {
        if (!_projectsRepository.IsConnected)
        {
            return;
        }

        try
        {
            await _projectsRepository.SaveAsync(_projects, CancellationToken.None);
        }
        catch (Exception ex)
        {
            BeginInvoke(() => lblStatus.Text = $"Cloud project sync failed: {ex.Message}");
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _appService.StateChanged -= AppServiceOnStateChanged;
        _appService.EventsChanged -= AppServiceOnEventsChanged;
        _appService.Dispose();
        _projectsRepository.Dispose();
        _reconnectTimer?.Dispose();
        _trayIcon?.Dispose();
        _trayMenu?.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    private static bool HasTrayStartupArg()
    {
        string[] args = Environment.GetCommandLineArgs();
        return args.Skip(1).Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
    }

    private void InitializeTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        };


        _trayProjectsSeparator = new ToolStripSeparator();
        _trayMenu.Items.Add(_trayProjectsSeparator);

        ToolStripMenuItem trayNoneItem = new("Set none");
        trayNoneItem.Click += async (_, _) => await SafeEmitIntentAsync(StateIntent.None());
        _trayMenu.Items.Add(trayNoneItem);

        _trayMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem showItem = new("Show window");
        showItem.Click += (_, _) =>
        {
            _trayMenu?.Close(ToolStripDropDownCloseReason.CloseCalled);
            ShowFromTray();
        };
        _trayMenu.Items.Add(showItem);

        _trayAutostartItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled()
        };
        _trayAutostartItem.CheckedChanged += TrayAutostartItem_CheckedChanged;
        _trayMenu.Items.Add(_trayAutostartItem);

        ToolStripMenuItem pinTrayItem = new("Always show tray icon… (Windows settings)");
        pinTrayItem.Click += (_, _) =>
        {
            _trayMenu?.Close(ToolStripDropDownCloseReason.CloseCalled);
            OpenTaskbarSettings();
        };
        _trayMenu.Items.Add(pinTrayItem);

        _trayMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem exitItem = new("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayMenu?.Close(ToolStripDropDownCloseReason.CloseCalled);
            _exitRequested = true;
            Close();
        };
        _trayMenu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "ProjectTimeTracker",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        UpdateTrayIconImage(null);
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowFromTray();
            }
        };

        RebuildTrayProjectsMenu();
        ShowFirstRunPinHintIfNeeded();
    }

    private static void OpenTaskbarSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:taskbar",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback: ignore; user can open Settings manually.
        }
    }

    private string PinHintFlagPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProjectTimeTracker",
        "tray-pin-hint.flag");

    private void ShowFirstRunPinHintIfNeeded()
    {
        try
        {
            if (File.Exists(PinHintFlagPath) || _trayIcon is null)
            {
                return;
            }

            // Delay a moment so the icon is registered before showing the balloon.
            System.Windows.Forms.Timer t = new() { Interval = 1500 };
            t.Tick += (_, _) =>
            {
                t.Stop();
                t.Dispose();
                try
                {
                    _trayIcon?.ShowBalloonTip(
                        8000,
                        "Pin ProjectTimeTracker to the taskbar",
                        "Open Taskbar settings → Other system tray icons → enable ProjectTimeTracker so the icon stays visible.",
                        ToolTipIcon.Info);
                    Directory.CreateDirectory(Path.GetDirectoryName(PinHintFlagPath)!);
                    File.WriteAllText(PinHintFlagPath, DateTime.UtcNow.ToString("o"));
                }
                catch
                {
                    // Non-fatal.
                }
            };
            t.Start();
        }
        catch
        {
            // Non-fatal.
        }
    }

    private void TrayAutostartItem_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            if (_trayAutostartItem!.Checked)
            {
                StartupRegistration.Enable(Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
            }
            else
            {
                StartupRegistration.Disable();
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Autostart change failed: {ex.Message}";
        }
    }

    private void TryEnableAutostartByDefault()
    {
        try
        {
            if (!StartupRegistration.IsEnabled())
            {
                StartupRegistration.Enable(Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
                if (_trayAutostartItem is not null)
                {
                    _trayAutostartItem.Checked = true;
                }
            }
        }
        catch
        {
            // Non-fatal: user can toggle from tray menu.
        }
    }

    private void RebuildTrayProjectsMenu()
    {
        if (_trayMenu is null || _trayProjectsSeparator is null)
        {
            return;
        }

        foreach (ToolStripItem item in _trayProjectItems)
        {
            _trayMenu.Items.Remove(item);
            item.Dispose();
        }
        _trayProjectItems.Clear();

        _trayProjectsSeparator.Visible = _projects.Count > 0;

        int insertIndex = _trayMenu.Items.IndexOf(_trayProjectsSeparator);
        if (insertIndex < 0)
        {
            return;
        }

        foreach (string project in _projects)
        {
            string captured = project;
            ToolStripMenuItem item = new(captured) { Tag = captured };
            item.Click += async (_, _) => await SafeEmitIntentAsync(StateIntent.ForProject(captured));
            _trayMenu.Items.Insert(insertIndex, item);
            _trayProjectItems.Add(item);
            insertIndex++;
        }

        UpdateTrayActiveStyles();
    }

    private async Task SafeEmitIntentAsync(StateIntent intent)
    {
        try
        {
            await _appService.EmitIntentAsync(intent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _trayIcon?.ShowBalloonTip(2000, "ProjectTimeTracker", ex.Message, ToolTipIcon.Warning);
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _trayIcon?.ShowBalloonTip(1500, "ProjectTimeTracker", "Still running in the tray.", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private static TimeZoneInfo ResolveMontrealTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"); }
        catch { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        catch { }
        return TimeZoneInfo.Local;
    }

    private static DateTime ToMontreal(DateTime utc)
    {
        DateTime asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, MontrealTimeZone);
    }

    private IntPtr _currentTrayHIcon = IntPtr.Zero;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void UpdateTrayIconImage(string? activeProject)
    {
        if (_trayIcon is null)
        {
            return;
        }

        Color color = GetProjectColor(activeProject);
        IntPtr newHIcon;
        Icon newIcon = BuildSolidIcon(color, activeProject, out newHIcon);
        _trayIcon.Icon = newIcon;

        // Free the previous unmanaged icon handle (only if we created it).
        if (_currentTrayHIcon != IntPtr.Zero)
        {
            DestroyIcon(_currentTrayHIcon);
        }
        _currentTrayHIcon = newHIcon;
    }

    private static Color GetProjectColor(string? activeProject)
    {
        if (string.IsNullOrWhiteSpace(activeProject))
        {
            return Color.Gray;
        }
        if (string.Equals(activeProject, "Scolago", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromArgb(255, 140, 0); // orange
        }
        if (string.Equals(activeProject, "Comunik", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromArgb(0, 110, 200); // blue
        }
        return DeterministicColor(activeProject);
    }

    private static Color DeterministicColor(string project)
    {
        int hash = 0;
        foreach (char c in project)
        {
            hash = unchecked(hash * 31 + char.ToLowerInvariant(c));
        }
        int hue = Math.Abs(hash) % 360;
        return HslToRgb(hue, 0.65, 0.5);
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r = 0, g = 0, b = 0;
        if (hp < 1) { r = c; g = x; }
        else if (hp < 2) { r = x; g = c; }
        else if (hp < 3) { g = c; b = x; }
        else if (hp < 4) { g = x; b = c; }
        else if (hp < 5) { r = x; b = c; }
        else { r = c; b = x; }
        double m = l - c / 2;
        return Color.FromArgb(
            Math.Clamp((int)Math.Round((r + m) * 255), 0, 255),
            Math.Clamp((int)Math.Round((g + m) * 255), 0, 255),
            Math.Clamp((int)Math.Round((b + m) * 255), 0, 255));
    }

    private static Icon BuildSolidIcon(Color color, string? project, out IntPtr hIcon)
    {
        const int size = 32;
        using Bitmap bmp = new(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using SolidBrush fill = new(color);
            g.FillEllipse(fill, 1, 1, size - 2, size - 2);

            using Pen border = new(Color.FromArgb(160, 0, 0, 0), 1.5f);
            g.DrawEllipse(border, 1, 1, size - 2, size - 2);

            if (!string.IsNullOrWhiteSpace(project))
            {
                char letter = char.ToUpperInvariant(project!.Trim()[0]);
                Color textColor = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) < 140
                    ? Color.White
                    : Color.Black;
                using Font font = new("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
                using SolidBrush textBrush = new(textColor);
                using StringFormat sf = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(letter.ToString(), font, textBrush, new RectangleF(0, 0, size, size), sf);
            }
        }

        hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
