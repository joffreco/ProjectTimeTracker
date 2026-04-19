# Secrets

Place your Google OAuth **desktop** client secret here as:

```
Secrets/client_secret.json
```

This file is embedded into the built EXE as a resource (logical name
`ProjectTimeTracker.client_secret.json`) so the app is self-contained and the
end-user no longer needs to pick a JSON file at runtime.

The file is git-ignored. If it is missing at build time, the build still
succeeds but the app will fail to connect at startup with a clear error.

> Security note: Google's "desktop app" OAuth client secret is not considered
> truly confidential (Google's docs explicitly say so), but anyone with the
> EXE can extract it. Only embed a secret you're comfortable distributing
> with the binary.

