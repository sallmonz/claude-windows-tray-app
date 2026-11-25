# Claude Usage Tray

A small Windows system tray app that shows your Claude usage limits:

- 5-hour window: percent used and when it resets
- Weekly (7-day) window: percent used and when it resets
- When the data was last fetched

The tray icon shows the 5-hour percent (green < 50%, orange 50–80%, red > 80%, gray when data is stale).
Left-click opens the popup. Right-click opens the menu: **Refresh now**, **Start with Windows**, **Exit**.
The popup follows the Windows light/dark theme and can switch between English and Thai.

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (to run)
- .NET 10 SDK (to build)
- Claude Code, logged in with a Claude account. The app reads the token from
  `%USERPROFILE%\.claude\.credentials.json`.

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

## Limitations

- **Unofficial API.** The app calls `https://api.anthropic.com/api/oauth/usage`, which is not a public
  or documented API. It can change or stop working at any time. If the format changes, the popup says so.
  All API knowledge is in `UsageClient.cs` and `UsageParser.cs`, so a fix only touches those files.
- The app never refreshes the token itself. If the token expires, open Claude Code and it will refresh it.
- Data refreshes every 3 minutes.

## Files

| Path | What |
| --- | --- |
| `%LOCALAPPDATA%\ClaudeUsageTray\log.txt` | Error log |
| `%LOCALAPPDATA%\ClaudeUsageTray\settings.json` | Language setting |
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
