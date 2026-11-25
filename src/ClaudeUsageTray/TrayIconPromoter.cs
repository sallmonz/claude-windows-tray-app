using Microsoft.Win32;

namespace ClaudeUsageTray;

/// <summary>
/// Moves the tray icon out of the ^ overflow once, when the installer asked for it
/// (HKCU\Software\ClaudeUsageTray\PromoteTrayIcon = 1).
///
/// Windows 11 keeps one key per tray icon under HKCU\Control Panel\NotifyIconSettings and shows the icon
/// on the taskbar when IsPromoted = 1. There is no public API for this. Explorer creates the key only after
/// the icon first appears, so this retries for a while. The flag is cleared after one success, so if the
/// user later hides the icon again, the app respects that.
/// </summary>
internal sealed class TrayIconPromoter : IDisposable
{
    private const string FlagKey = @"Software\ClaudeUsageTray";
    private const string FlagValue = "PromoteTrayIcon";
    private const string IconSettingsKey = @"Control Panel\NotifyIconSettings";
    private const int MaxAttempts = 24; // about 2 minutes

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };
    private int _attempts;

    public void Start()
    {
        if (!IsRequested())
            return;
        _timer.Tick += (_, _) => TryOnce();
        _timer.Start();
    }

    private void TryOnce()
    {
        _attempts++;
        try
        {
            if (Promote())
            {
                ClearRequest();
                Logger.Write("Tray icon moved to the taskbar.");
                _timer.Stop();
            }
            else if (_attempts >= MaxAttempts)
            {
                Logger.Write("Tray icon settings key not found; icon stays where Windows put it.");
                _timer.Stop();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Cannot move tray icon to the taskbar", ex);
            _timer.Stop();
        }
    }

    private static bool IsRequested()
    {
        using var key = Registry.CurrentUser.OpenSubKey(FlagKey);
        return key?.GetValue(FlagValue) is int v && v != 0;
    }

    private static void ClearRequest()
    {
        using var key = Registry.CurrentUser.OpenSubKey(FlagKey, writable: true);
        key?.DeleteValue(FlagValue, throwOnMissingValue: false);
    }

    /// <summary>Sets IsPromoted on every icon entry for this exe. Paths can use known-folder GUIDs, so match by file name.</summary>
    private static bool Promote()
    {
        var exeName = Path.GetFileName(Environment.ProcessPath) ?? "ClaudeUsageTray.exe";
        using var root = Registry.CurrentUser.OpenSubKey(IconSettingsKey);
        if (root is null)
            return false;

        bool found = false;
        foreach (var name in root.GetSubKeyNames())
        {
            using var entry = root.OpenSubKey(name, writable: true);
            if (entry?.GetValue("ExecutablePath") is string path &&
                path.EndsWith("\\" + exeName, StringComparison.OrdinalIgnoreCase))
            {
                entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                found = true;
            }
        }
        return found;
    }

    public void Dispose() => _timer.Dispose();
}
