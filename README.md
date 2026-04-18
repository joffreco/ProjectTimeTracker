# FirestoreDesktopMini

Minimal WinForms (.NET 9) desktop app that connects to Google Firestore using an OAuth Desktop Client JSON (`apps.googleusercontent.com.json`).

## What it does

- Lets you select your OAuth desktop client JSON file.
- Opens Google sign-in flow.
- Connects to Firestore using your authenticated user token.
- Lists up to 20 document IDs from a collection.
- Adds a sample document.

## Prerequisites

- .NET SDK 9.0+
- A Google Cloud project with:
  - Firestore database created
  - Firestore API enabled
  - OAuth desktop client created (your JSON file)
- Your signed-in Google account must have Firestore access permissions in that project.

## Run

```powershell
dotnet run --project .\FirestoreDesktopMini.csproj
```

## Notes

- On first connect, a browser sign-in/consent screen appears.
- OAuth tokens are cached in:
  - `%LOCALAPPDATA%\FirestoreDesktopMini.Auth`
- Default collection name is `demo`.

## Troubleshooting

- `project_id is missing in the JSON file`:
  - Make sure you selected a **Desktop OAuth client** JSON.
- Permission errors:
  - Grant your Google account the needed Firestore role in IAM.
- No documents shown:
  - Try `Add sample document`, then `Load documents`.
