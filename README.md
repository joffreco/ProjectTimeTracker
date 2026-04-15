# ProjectTimeTracker

A Windows desktop application (WPF / .NET 9) that tracks time spent on projects using an event-sourced, state-based system with **real-time multi-device sync** via Firebase Firestore.

## Features

- **State-based tracking** — switch between `None`, `Project1`, `Project2`
- **Event sourcing** — every state change stored as an immutable event
- **Real-time sync** — Firestore Realtime Listeners keep all devices in sync (< 100 ms)
- **System tray** — minimises to the notification area; keeps running in background
- **Offline-first** — works offline, syncs when connectivity is restored
- **Structured logging** — Serilog → console + daily rolling file

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 9.0+ |
| Windows | 10 / 11 |
| Firebase project | with Firestore enabled |

## Quick Start

### 1. Clone & restore

```bash
git clone <repo-url>
cd ProjectTimeTracker
dotnet restore
```

### 2. Configure Firebase

1. Create a Firebase project at <https://console.firebase.google.com>
2. Enable **Cloud Firestore** (Native mode)
3. Generate a **Service Account key** (JSON) from *Project Settings → Service Accounts*
4. Copy the key file to `src/ProjectTimeTracker/serviceAccountKey.json`
5. Edit `src/ProjectTimeTracker/appsettings.json`:

```json
{
  "Firestore": {
    "ProjectId": "your-firebase-project-id",
    "CredentialsPath": "serviceAccountKey.json",
    "Collection": "events"
  }
}
```

> ⚠️ `serviceAccountKey.json` is git-ignored — never commit credentials.

### 3. Build & run

```bash
dotnet build
dotnet run --project src/ProjectTimeTracker
```

### 4. Run tests

```bash
dotnet test
```

## Architecture

```
src/ProjectTimeTracker/
├── Domain/            State enum, StateEvent (Firestore data class)
├── Services/          IFirestoreService, IStateManager + implementations
├── ViewModels/        MainViewModel (MVVM), RelayCommand
├── Configuration/     FirestoreOptions (strongly-typed config)
├── App.xaml(.cs)      DI container, Serilog, Firestore bootstrap
└── MainWindow.xaml(.cs)  WPF UI + system tray (NotifyIcon)

tests/ProjectTimeTracker.Tests/
├── StateEventTests.cs
└── StateManagerTests.cs
```

### Key principles

- **Append-only** — events are never overwritten
- **Explicit types** — no `var`; PascalCase everywhere
- **async/await** — no `.Result` or `.Wait()`
- **Dependency injection** — services injected via `Microsoft.Extensions.DependencyInjection`
- **Separation of concerns** — UI → ViewModel → Services → Firestore

## System Tray Behaviour

| Action | Result |
|---|---|
| Click **X** (close) | Hides to system tray |
| Click **Minimise** | Hides to system tray |
| Left-click tray icon | Toggle window visibility |
| Right-click tray icon → **Show** | Restore window |
| Right-click tray icon → **Exit** | Graceful shutdown |

## Logging

Logs are written to:
- **Console** (debug)
- **`logs/log-YYYY-MM-DD.txt`** (rolling daily, 7 days retention)

## License

Private project — all rights reserved.

