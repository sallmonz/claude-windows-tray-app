# Claude Usage Tray

A small Windows system tray app that shows your Claude usage limits:

- 5-hour window: percent used and when it resets
- Weekly (7-day) window: percent used and when it resets
- When the data was last fetched

The tray icon shows the 5-hour percent by default (green < 50%, orange 50–80%, red > 80%, gray when data is stale).
The right-click menu lets you pick which window the icon shows (5-hour or weekly) and whether it shows a small "%".
Left-click opens the popup. Right-click opens the menu: **Refresh now**, icon options, **Start with Windows**, **Exit**.
The popup follows the Windows light/dark theme until you pick light or dark with the sun/moon switch, and can switch between English and Thai.

## Download and install

1. Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64), if you do not have it.
2. Download `ClaudeUsageTray.exe` from the [latest release](../../releases/latest).
3. Put it in a folder that will not move, for example `%LOCALAPPDATA%\Programs\ClaudeUsageTray\`.
4. Run it, then right-click the tray icon and turn on **Start with Windows**.

The exe is not code-signed, so Windows SmartScreen may warn the first time. Click **More info**, then **Run anyway**.

### Check the download (optional)

Each release has a `ClaudeUsageTray.exe.sha256` file and a GitHub build attestation.

```powershell
Get-FileHash .\ClaudeUsageTray.exe -Algorithm SHA256   # compare with the .sha256 file
gh attestation verify .\ClaudeUsageTray.exe --repo sallmonz/claude-windows-tray-app
```

The attestation proves the exe was built by this repository's `Release` workflow from public source.

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (to run)
- .NET 10 SDK (to build)
- **Claude Code CLI**, logged in with a Claude account (`claude`, then `/login`). The app reads the token from
  `%USERPROFILE%\.claude\.credentials.json` (or `%CLAUDE_CONFIG_DIR%\.credentials.json`).

> **Using Claude Code only inside the Claude desktop app?** The desktop app keeps its own login and does not
> write a usable token to `.credentials.json` (the file has an empty `accessToken`). Install the CLI and log in
> once, then the tray app can read the token.

## Build and test

```bash
dotnet build
dotnet test
```

Run it:

```bash
dotnet run --project src/ClaudeUsageTray
```

## Publish

Creates one `ClaudeUsageTray.exe` (framework-dependent, win-x64):

```bash
dotnet publish src/ClaudeUsageTray -p:PublishProfile=win-x64
```

Output: `src/ClaudeUsageTray/bin/publish/win-x64/ClaudeUsageTray.exe`

## Make a release (maintainers)

On GitHub, go to **Releases > Draft a new release**, create a tag like `v1.0.0`, click
**Generate release notes**, then **Publish release**. The `Release` workflow builds the exe and attaches it.

## Limitations

- **Unofficial API.** The app calls `https://api.anthropic.com/api/oauth/usage`, which is not a public
  or documented API. It can change or stop working at any time. If the format changes, the popup says so.
  All API knowledge is in `UsageClient.cs` and `UsageParser.cs`, so a fix only touches those files.
- The app never refreshes the token itself, so it cannot log out your Claude Code session. The CLI refreshes the
  token when it runs. If the popup says the token expired, run `claude` in a terminal once.
- Data refreshes every 3 minutes.

## Files

| Path | What |
| --- | --- |
| `%LOCALAPPDATA%\ClaudeUsageTray\log.txt` | Error log |
| `%LOCALAPPDATA%\ClaudeUsageTray\settings.json` | Language, theme and icon settings |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ClaudeUsageTray` | Start with Windows |

## Project layout

```
src/ClaudeUsageTray.Core/   Models, JSON parsing, countdown text, UsageClient (no UI, cross-platform)
src/ClaudeUsageTray/        WinForms tray app: Program, TrayApp, UsagePopup, IconRenderer
tests/ClaudeUsageTray.Tests xUnit tests for the Core project
```

## Dependencies and license

The app uses only the .NET base class library, with no extra NuGet packages.
The test project uses xUnit (Apache-2.0) and Microsoft.NET.Test.Sdk (MIT). These are not part of the app.

This project is released under the [MIT License](LICENSE). It is not affiliated with Anthropic.
