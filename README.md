# ProjectTimeTracker

Small WinForms (.NET 9) app to track your active project state (`project name` or `none`) using event sourcing.

The app is append-only:
- UI emits transition intents (`Start/Switch`, `Set none`).
- Events are persisted locally first (offline queue).
- A background worker syncs queued events to Firestore with exponential backoff.
- Firestore listeners update all connected Windows PCs in near real time.

## Prerequisites

- .NET SDK 9.0+
- A Google Cloud project with:
  - Firestore database created
  - Firestore API enabled
  - OAuth desktop client created (your JSON file)
- Your signed-in Google account must have Firestore access permissions in that project.

## Run

```powershell
dotnet run --project .\ProjectTimeTracker.csproj
```

## Notes

- On first connect, a browser sign-in/consent screen appears.
- OAuth tokens are cached in `%LOCALAPPDATA%\ProjectTimeTracker.Auth`.
- Local event queue is stored in `%LOCALAPPDATA%\ProjectTimeTracker\event-queue.json`.
- Device identity is stored in `%LOCALAPPDATA%\ProjectTimeTracker\device.id`.
- Firestore event stream path is `timeTrackerUsers/{userId}/events`.

## Troubleshooting

- `project_id is missing in the JSON file`:
  - Make sure you selected a Desktop OAuth client JSON.
- Permission errors:
  - Grant your Google account Firestore permissions in IAM.
- Events not syncing:
  - Keep the app running to let the background worker retry queued events.
