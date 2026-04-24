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

## Build Portable EXE (USB)

Generate a single-file framework-dependent `.exe` you can copy to a USB key.

Target PCs must have the .NET 9 Windows Desktop Runtime installed.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1 -Version 1.0.0 -Rid win-x64
```

Output file:
- `artifacts\usb-ready\ProjectTimeTracker_v1.0.0_win-x64_framework-dependent.zip`

The zip contains:
- `ProjectTimeTracker_v1.0.0_win-x64_framework-dependent.exe`
- `ProjectTimeTracker_v1.0.0_win-x64_framework-dependent.exe.sha256`

Verify checksum:

```powershell
Expand-Archive .\artifacts\usb-ready\ProjectTimeTracker_v1.0.0_win-x64_framework-dependent.zip -DestinationPath .\artifacts\usb-ready\extracted -Force
Get-FileHash .\artifacts\usb-ready\extracted\ProjectTimeTracker_v1.0.0_win-x64_framework-dependent.exe -Algorithm SHA256
Get-Content .\artifacts\usb-ready\extracted\ProjectTimeTracker_v1.0.0_win-x64_framework-dependent.exe.sha256
```

## Notes

- On first connect, a browser sign-in/consent screen appears.
- OAuth tokens are cached in `%LOCALAPPDATA%\ProjectTimeTracker.Auth`.
- Local event queue is stored in `%LOCALAPPDATA%\ProjectTimeTracker\event-queue.json`.
- Device identity is stored in `%LOCALAPPDATA%\ProjectTimeTracker\device.id`.
- Firestore events live in the top-level `timeTrackerEvents` collection. Each document carries a `userId` field; queries and listeners filter by it. Document id = event GUID (`N` format), so events stay globally unique across users and devices.
- On the first connect after upgrading, any events still found under the legacy path `timeTrackerUsers/{userId}/events` are automatically migrated (copied to `timeTrackerEvents/{eventId}` then deleted from the legacy sub-collection) inside batched writes. The migration is idempotent and resumable: if it is interrupted, the next connect picks up where it left off.
- Other per-user state (e.g. `timeTrackerUsers/{userId}/state/projects`) still lives under `timeTrackerUsers/{userId}` and is unchanged.

## Firestore setup for the top-level events collection

Two things must be aligned in your Firestore project after upgrading:

1. **Composite index.** The events query is
   `where userId == <me> order by occurredAtUtc asc, eventId asc`.
   On the first run Firestore will throw a `FailedPrecondition` error containing a
   direct link to create the required composite index — open it once and confirm.
2. **Security rules.** Grant the signed-in user read/write only to their own events
   in the new collection. Example rule:

   ```
   match /timeTrackerEvents/{eventId} {
     allow read, write: if request.auth != null
       && request.resource.data.userId == request.auth.uid
       && resource.data.userId == request.auth.uid;
   }
   ```

   Adjust to match how you identify users (this project uses your Google account id
   as `userId`).

## Troubleshooting

- `project_id is missing in the JSON file`:
  - Make sure you selected a Desktop OAuth client JSON.
- Permission errors:
  - Grant your Google account Firestore permissions in IAM.
- Events not syncing:
  - Keep the app running to let the background worker retry queued events.
