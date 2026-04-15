# Copilot Instructions — Firestore Event Tracking Desktop App

> **Implementation Plan**: Build a layered, event-sourced time tracker with offline-first capabilities and Firestore persistence.

## Context

This project is a Windows desktop application built with C# (.NET 9, WPF or WinUI).

The application tracks time spent on projects using a state-based system for a **single user on multiple devices**:

* NONE
* PROJECT1
* PROJECT2

Each state change generates an immutable event that is persisted locally and to Firestore, and syncs **in real-time across all devices**.

> **Single-User, Multi-Device Design**: One user, multiple Windows devices. All devices share the same event stream and display synchronized state. No user management, no large event logs. Focus on reliability and real-time synchronization.

---

## Architecture Principles

> **PHASE 1 — Foundation**: These principles ensure the system remains scalable, auditable, and recoverable.

* Use an **event sourcing pattern**
  - *Why*: Every state change is immutable; reconstruction of state is always possible
* Never store aggregated durations directly
  - *Why*: Durations are derived; always compute from event timestamps on read
* Always store **state transitions as immutable events**
  - *Why*: Immutability ensures data integrity and simplifies offline sync
* The system must be **append-only**
  - *Why*: Prevents accidental data loss and maintains audit trail

---

## Firestore Integration

> **PHASE 2 — Persistence Layer**: Standard Firestore SDK with WebSocket Realtime Listeners.

* ALWAYS use Firebase Admin SDK for .NET
  - *Why*: Native support for Realtime Listeners; built-in conflict resolution; offline persistence
* Use Firestore Realtime Listen
  - *Why*: Real-time data < 100ms; automatic offline queuing; native WebSocket support
* Use async/await everywhere
  - *Why*: Prevents UI blocking; allows graceful degradation when offline
* Enable offline persistence
  - *Why*: Local cache automatically syncs when connectivity restored

Firebase setup:

```csharp
var credential = GoogleCredential.FromFile("path/to/serviceAccountKey.json");
var app = FirebaseApp.Create(new AppOptions { Credential = credential });
var firestore = FirestoreDb.GetInstance(app);
```

> **Note**: Store service account key in secure config; never commit to source control

---

## Firebase Authentication

> **PHASE 1 — Identity**: Authenticate via Firebase Auth (Email/Password) before accessing Firestore.

* Use **Firebase Authentication with Email and Password**
  - *Why*: Single user, but credentials secure the Firestore connection; prevents unauthorized access from other devices or actors
* Authenticate on app startup, before any Firestore operation
  - *Flow*: App launches → show login screen (if no cached token) → sign in with email/password → obtain ID token → use token for Firestore access
* Use the **Firebase Auth REST API** (`identitytoolkit.googleapis.com`)
  - *Why*: No official Firebase Auth SDK for .NET desktop; the REST API is lightweight and well-documented
  - *Endpoint*: `https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={API_KEY}`
* **Token management**:
  - Store the ID token and refresh token securely in memory (and optionally encrypted on disk for auto-login)
  - ID tokens expire after 1 hour; use the refresh token to obtain a new one automatically
  - *Refresh endpoint*: `https://securetoken.googleapis.com/v1/token?key={API_KEY}`
* **Firestore Security Rules** must require authentication:
  ```
  rules_version = '2';
  service cloud.firestore {
    match /databases/{database}/documents {
      match /events/{event} {
        allow read, write: if request.auth != null;
      }
    }
  }
  ```
* Store **Firebase API Key** and **email/password** in `appsettings.json` (API key) and secure storage (credentials)
  - *Never* hardcode credentials in source code
  - API key is safe to include in config (it is not a secret on its own; Firestore rules enforce access)

### Authentication Service

Dedicated service class:

* **Interface**: `IAuthService`
* **Class**: `FirebaseAuthService`
* **Methods**:
  * `Task<AuthToken> SignInAsync(string email, string password)` — Authenticate and return tokens
  * `Task<AuthToken> RefreshTokenAsync(string refreshToken)` — Refresh an expired ID token
  * `bool IsAuthenticated { get; }` — Whether a valid (non-expired) token is held
  * `string? GetIdToken()` — Return current ID token for Firestore calls

### AuthToken Model

```csharp
public class AuthToken
{
    public string IdToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

### Configuration (appsettings.json)

```json
{
  "Firebase": {
    "ApiKey": "your-firebase-web-api-key",
    "Email": "your-email@example.com",
    "Password": "stored-securely-or-prompted"
  }
}
```

> **Note**: For production, prompt the user for password at first launch and store the refresh token encrypted on disk. Avoid storing plaintext passwords in config.

### Startup Flow

1. App starts → `IAuthService.SignInAsync()` called
2. On success → ID token stored in memory → `FirestoreDb` initialised with authenticated credentials
3. On failure → show error in UI; allow retry
4. Background timer refreshes the token before expiry (every ~55 minutes)
5. All Firestore calls include the valid ID token

---

## Firestore Document Format

When writing documents, use strongly-typed C# classes with FirebaseFirestore attributes:

```csharp
[FirestoreData]
public class StateEvent
{
  [FirestoreProperty]
  public string FromState { get; set; }
  
  [FirestoreProperty]
  public string ToState { get; set; }
  
  [FirestoreProperty]
  public Timestamp Timestamp { get; set; }
}
```

* Use `Timestamp` type for Firestore server timestamps
* Always use UTC
* Firestore automatically serializes/deserializes C# objects

Native Firestore read/write:

```csharp
// Write
await firestore.Collection("events").AddAsync(stateEvent);

// Read (Realtime Listener)
firestore.Collection("events")
  .Listen(snapshot =>
  {
    foreach (DocumentChange change in snapshot.Changes)
    {
      StateEvent evt = change.Document.ConvertTo<StateEvent>();
      // Handle event
    }
  });
```

---

## Domain Model

Always generate strongly typed C# classes with Firestore attributes:

* Use explicit types (no var)
* Use PascalCase
* Add `[FirestoreData]` and `[FirestoreProperty]` attributes

Example:

```csharp
public enum State
{
  None,
  Project1,
  Project2
}

[FirestoreData]
public class StateEvent
{
  [FirestoreProperty]
  public string FromState { get; set; }
  
  [FirestoreProperty]
  public string ToState { get; set; }
  
  [FirestoreProperty]
  public Timestamp Timestamp { get; set; }
}
```

---

## State Management Rules

> **PHASE 2 — State Reconciliation**: Maintain consistency between local memory and Firestore via WebSocket listeners.

* Ignore duplicate transitions (same state → same state)
  - *Why*: Deduplicate on client; reduces noise in event log
* Ensure valid state transitions only
  - *Why*: Prevents invalid state combinations (e.g., PROJECT1 → PROJECT2 requires intermediate NONE)
* Maintain current state in memory
  - *Why*: Enables fast UI updates; reduces Firestore reads
  
> **Optimization**: Load last N events on startup to rebuild current state; cache in-memory state tree

---

## Required Patterns

> **PHASE 3 — Reliability**: Leverage Firestore's built-in offline persistence and Realtime Listeners.

### 1. Realtime Listeners for Multi-Device Sync

* ALWAYS use Firestore Realtime Listeners
  - *Why*: WebSocket-based; < 100ms latency; automatic reconnection; perfect for multi-device
* Handle listener events (ADDED, MODIFIED, REMOVED)
  - *Strategy*: All devices listen to the same event stream; state automatically syncs across devices
* Automatic offline persistence
  - *Why*: Firestore SDK caches locally; syncs on reconnect; each device has independent offline capability
  
> **Multi-Device Optimization**: When one device posts an event, all devices receive it via Realtime Listener < 100ms. Firestore ensures consistency.

### 2. Offline Support

* Enable offline persistence in Firestore config
  ```csharp
  var settings = new FirestoreSettings { PersistenceEnabled = true };
  var firestore = new FirestoreDb(app, "projectId", settings);
  ```
* Queue writes automatically (Firestore handles this)
* Sync when connectivity restored (automatic via Firestore)

> **Optimization**: Listen to connection state to show offline UI indicators

### 3. System Tray Integration (Background Service Mode)

* ALWAYS minimize to system tray instead of closing
  - *Why*: App continues running; maintains WebSocket connections alive; prevents data loss
* Implement NotifyIcon for Windows taskbar integration
  - *Why*: User can quickly access app from hidden icons; show current project state in tooltip
* Handle window minimize/close events
  - *Logic*: Minimize button → Hide to tray; Close button (X) → Hide to tray; Tray icon click → Show/hide window
* Prevent accidental application exits
  - *Strategy*: Close button minimizes to tray; only exit via explicit "Exit" menu option
* Support quick access from taskbar
  - *Implementation*: Right-click context menu with "Show" and "Exit" options

Example (WPF implementation):

```csharp
public partial class MainWindow : Window
{
  private NotifyIcon _notifyIcon;
  
  public MainWindow()
  {
    InitializeComponent();
    InitializeSystemTray();
  }
  
  private void InitializeSystemTray()
  {
    _notifyIcon = new NotifyIcon
    {
      Icon = SystemIcons.Application,
      Text = "ProjectTimeTracker - Running",
      Visible = true,
      ContextMenu = CreateContextMenu()
    };
    
    _notifyIcon.MouseClick += (s, e) =>
    {
      if (e.Button == MouseButtons.Left)
      {
        ToggleWindowVisibility();
      }
    };
  }
  
  private ContextMenu CreateContextMenu()
  {
    ContextMenu menu = new ContextMenu();
    
    MenuItem showItem = new MenuItem("Show");
    showItem.Click += (s, e) => ToggleWindowVisibility();
    menu.MenuItems.Add(showItem);
    
    menu.MenuItems.Add("-");
    
    MenuItem exitItem = new MenuItem("Exit");
    exitItem.Click += (s, e) => ExitApplication();
    menu.MenuItems.Add(exitItem);
    
    return menu;
  }
  
  private void ToggleWindowVisibility()
  {
    if (this.IsVisible && this.WindowState == WindowState.Normal)
    {
      this.Hide();
      this.WindowState = WindowState.Minimized;
    }
    else
    {
      this.Show();
      this.WindowState = WindowState.Normal;
      this.Activate();
      this.Topmost = true;
      this.Topmost = false;
    }
  }
  
  private void Window_StateChanged(object sender, EventArgs e)
  {
    if (this.WindowState == WindowState.Minimized)
    {
      this.Hide();
    }
  }
  
  private void Window_Closing(object sender, CancelEventArgs e)
  {
    // Close button minimizes to tray instead of exiting
    e.Cancel = true;
    this.WindowState = WindowState.Minimized;
  }
  
  private void ExitApplication()
  {
    _notifyIcon?.Dispose();
    this.Close();
    Application.Current.Shutdown();
  }
  
  protected override void OnClosed(EventArgs e)
  {
    base.OnClosed(e);
    _notifyIcon?.Dispose();
  }
}
```

> **XAML**: Add `WindowStyle="SingleBorderWindow"` and `ShowInTaskbar="True"` to main window
> **NuGet**: Add `System.Windows.Forms` reference for NotifyIcon

---

## Code Style Requirements

> **PHASE 1 — Core Patterns**: Enforce consistency and testability.

* Use explicit types (no `var`)
  - *Example*: `StateEvent evt = new StateEvent();` not `var evt = ...`
* Use async/await
  - *Never*: `.Result`, `.Wait()`, `Task.Run()`
* Use dependency injection where appropriate
  - *Container*: Microsoft.Extensions.DependencyInjection
  - *Services*: IFirestoreService (facade), IStateManager
* Separate concerns:

  * **UI Layer**: WPF/WinUI components; state binding only; no Firestore calls
  * **Domain Layer**: StateEvent, State enum; validation; no external deps
  * **Service Layer**: FirestoreService (Firestore SDK wrapper), FirebaseAuthService (Auth REST API), StateManager, RealtimeSyncService

---

## Firestore Service

> **PHASE 3 — Integration Point**: Centralized, testable Firestore SDK wrapper.

Always generate a dedicated service class:

* **Class**: `FirestoreService` (wraps Firestore SDK)
* **Methods**:
  * `Task<DocumentReference> SendEventAsync(StateEvent evt)` — Write single event, returns DocumentRef
  * `ListenerRegistration ListenToEventsAsync(Action<List<StateEvent>> onSnapshot)` — Realtime listener for multi-device sync
  * `Task<List<StateEvent>> GetEventsAsync(int limit = 100)` — Fetch recent events for state rebuild
  * `Task<bool> TestConnectivityAsync()` — Verify connection to Firestore

> **Implementation Notes**:
> - Inject `FirestoreDb` (initialized in dependency container)
> - Automatically includes ServerTimestamp on write
> - Listener handles offline buffering transparently
> - Parse DocumentSnapshot directly to typed StateEvent objects

---

## Logging

> **PHASE 3 — Observability**: Enable debugging and audit trails.

* Log all Firestore operations
  - *Include*: Operation type, document path, action result
  - *Use*: `ILogger.LogInformation()` with structured properties
* Log listener events
  - *Include*: EventType (ADDED/MODIFIED/REMOVED), DocumentId, Timestamp
  - *Use*: `ILogger.LogInformation()`
* Log failures with full context
  - *Include*: Exception type, FirestoreException details, Stack trace
  - *Use*: `ILogger.LogError()` 
* Log state transitions
  - *Include*: FromState, ToState, Timestamp, EventId
* Log connectivity changes
  - *Include*: Connection state (online/offline), Timestamp

> **Optimization**: Use `Serilog` for structured logging; write to file + console for diagnostics

---

## Advanced Guidelines

> **Performance & Simplicity Optimizations** (Single-User Design):

* Leverage Firestore Realtime Listeners
  - *Why*: WebSocket-based; automatic reconnection; native offline support
  - *No polling needed*: Listeners push changes < 100ms
* Keep event store small and simple
  - *Strategy*: Store only recent events (last 100-200); older events archived if needed
  - *Why*: Single user = minimal data; no pagination complexity needed
* Design for simplicity, not scalability
  - *Focus*: Reliable local state management; straightforward Firestore sync
  - *Avoid*: Complex partitioning, multi-user queries, event log pagination
  
> **Performance Targets** (Single-User):
> - State change latency: < 100ms (Realtime Listener delivery)
> - Firestore writes: < 50ms per document (SDK overhead)
> - Memory footprint: < 20MB (single user, no event log pagination)
> - Startup time: < 2 seconds (load last N events from local cache)

---

## What to Avoid

> **Common Pitfalls That Break Event Sourcing**:

* Do NOT compute total durations inside Firestore
  - *Why*: Durations must be derived on read from start/end timestamps
  - *Solution*: Compute on client-side; cache results with TTL
* Do NOT overwrite documents
  - *Why*: Breaks immutability and audit trail
  - *Solution*: Always append; use document IDs as timestamps
* Do NOT use blocking calls (.Result / .Wait())
  - *Why*: Deadlocks UI; causes thread pool starvation
  - *Solution*: Always use async/await; use Task composition
* Do NOT tightly couple UI and Firestore logic
  - *Why*: Prevents testing; makes offline support impossible
  - *Solution*: Use dependency injection; inject mocks for testing

> **Anti-Patterns to Flag in Code Review**:
> - Direct Firestore SDK calls outside FirestoreService
> - State mutations in UI event handlers
> - Manual JSON parsing (use ConvertTo<T> instead)
> - Firestore calls with hardcoded credentials
> - Polling instead of using Realtime Listeners

---

## Goal

Generate clean, production-ready, scalable code that follows:

* Event sourcing principles
* Real-time Firestore WebSocket integration
* Robust offline-first behavior

---

## Implementation Roadmap

### Sprint 1: Core Infrastructure
- [ ] Domain model (State enum, StateEvent class)
- [ ] **Firebase Authentication service (Email/Password via REST API)**
- [ ] **AuthToken model + token refresh logic**
- [ ] FirestoreService wrapper around SDK
- [ ] Realtime Listener setup
- [ ] Unit tests for domain validation
- [ ] Logging setup (Serilog)

### Sprint 2: Multi-Device Sync & Offline
- [ ] Firestore Realtime Listener integration
- [ ] Multi-device event stream sync
- [ ] Offline persistence enabled
- [ ] State snapshot on startup
- [ ] Integration tests with Firestore emulator

### Sprint 3: UI Integration & System Tray
- [ ] WPF/WinUI state binding
- [ ] UI event handlers (non-blocking)
- [ ] Dependency injection container setup
- [ ] Real-time state updates from Firestore listeners
- [ ] **System Tray integration (NotifyIcon)**
- [ ] **Minimize-to-tray behavior**
- [ ] **Context menu with Show/Exit options**
- [ ] End-to-end tests

### Sprint 4: Polish & Observability
- [ ] Structured logging review
- [ ] Performance profiling
- [ ] Connectivity state indicators
- [ ] Documentation

---

## Key Optimizations to Implement

| Optimization | Priority | Benefit | Effort |
|---|---|---|---|
| **System Tray Integration** | High | Background operation + quick access | Medium |
| **Realtime Listeners** | High | < 100ms multi-device sync | Low |
| **Offline Persistence** | High | Sync on reconnect | Low |
| **Serilog** | Medium | Structured debugging | Low |
| **State caching** | Medium | Sub-100ms UI updates | Low |
| **Composite indexes** | Medium | Efficient queries | Low |
| **Cursor pagination** | Low | Scale to large event logs | High |

---

## Testing Strategy

> **Unit Tests**: Domain model, state transitions, conflict resolution logic
> **Integration Tests**: FirestoreService with Firestore emulator
> **E2E Tests**: Full flow with Realtime Listeners, offline → online sync
> **Performance Tests**: Event processing throughput, memory footprint

---

## Security Checklist

- [ ] Store Firestore projectId in appsettings.json (never hardcode)
- [ ] Use service account credentials from secure config
- [ ] Enable Firebase Authentication (Email/Password) and enforce Firestore Security Rules (`request.auth != null`)
- [ ] Store Firebase API Key in appsettings.json; never store plaintext passwords in config
- [ ] Persist refresh token encrypted on disk; prompt for password on first launch only
- [ ] Refresh ID token automatically before expiry (~55 min timer)
- [ ] Never log sensitive data (tokens, passwords, user IDs)
- [ ] Validate all Firestore and Auth REST API responses
