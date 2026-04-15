using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectTimeTracker.Configuration;
using ProjectTimeTracker.Domain;
using ProjectTimeTracker.Services;

namespace ProjectTimeTracker.ViewModels;

/// <summary>
/// Main ViewModel — exposes current state and commands to the WPF UI.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private readonly IStateManager _stateManager;
    private readonly ILogger<MainViewModel> _logger;
    private readonly FirebaseAuthOptions _authOptions;

    private string _currentStateName = "None";
    private string _statusMessage = "Initializing…";
    private bool _isProject1Active;
    private bool _isProject2Active;
    private bool _isBusy;

    public MainViewModel(
        IAuthService authService,
        IStateManager stateManager,
        IOptions<FirebaseAuthOptions> authOptions,
        ILogger<MainViewModel> logger)
    {
        _authService = authService;
        _stateManager = stateManager;
        _authOptions = authOptions.Value;
        _logger = logger;

        SelectNoneCommand = new RelayCommand(async () => await SelectStateAsync(State.None), () => !IsBusy);
        SelectProject1Command = new RelayCommand(async () => await SelectStateAsync(State.Project1), () => !IsBusy);
        SelectProject2Command = new RelayCommand(async () => await SelectStateAsync(State.Project2), () => !IsBusy);

        _stateManager.StateChanged += OnStateChanged;
    }

    // ── Bindable properties ─────────────────────────────────────────────

    public string CurrentStateName
    {
        get => _currentStateName;
        private set => SetField(ref _currentStateName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsProject1Active
    {
        get => _isProject1Active;
        private set => SetField(ref _isProject1Active, value);
    }

    public bool IsProject2Active
    {
        get => _isProject2Active;
        private set => SetField(ref _isProject2Active, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetField(ref _isBusy, value);
            ((RelayCommand)SelectNoneCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SelectProject1Command).RaiseCanExecuteChanged();
            ((RelayCommand)SelectProject2Command).RaiseCanExecuteChanged();
        }
    }

    // ── Commands ────────────────────────────────────────────────────────

    public ICommand SelectNoneCommand { get; }
    public ICommand SelectProject1Command { get; }
    public ICommand SelectProject2Command { get; }

    // ── Public helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Called once from code-behind after the window is loaded.
    /// Authenticates first, then initialises state manager.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;

            // ── Step 1: Firebase Authentication ─────────────────────────
            StatusMessage = "Authenticating…";
            _logger.LogInformation("Starting Firebase Authentication");

            await _authService.SignInAsync(_authOptions.Email, _authOptions.Password);
            _authService.StartAutoRefresh();

            _logger.LogInformation("Authentication successful");

            // ── Step 2: Firestore initialisation ────────────────────────
            StatusMessage = "Connecting to Firestore…";
            await _stateManager.InitializeAsync();
            ApplyState(_stateManager.CurrentState);
            StatusMessage = "Connected — listening for changes";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialization failed");
            StatusMessage = $"Error — {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Private ─────────────────────────────────────────────────────────

    private async Task SelectStateAsync(State target)
    {
        try
        {
            IsBusy = true;
            await _stateManager.ChangeStateAsync(target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change state to {Target}", target);
            StatusMessage = $"Error — {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnStateChanged(State newState)
    {
        // Listener callback arrives on a background thread — dispatch to UI.
        System.Windows.Application.Current?.Dispatcher.Invoke(() => ApplyState(newState));
    }

    private void ApplyState(State state)
    {
        CurrentStateName = state.ToString();
        IsProject1Active = state == State.Project1;
        IsProject2Active = state == State.Project2;
        StatusMessage = state == State.None
            ? "No project selected"
            : $"Tracking: {state}";
    }

    // ── INotifyPropertyChanged ──────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

