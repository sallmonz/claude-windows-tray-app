using ClaudeUsageTray.Core;

namespace ClaudeUsageTray;

/// <summary>App root: tray icon, right-click menu, auto refresh and the popup. No main window.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(3);

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly UsageClient _client = new(Logger.Write);
    private readonly UsageState _state = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly UsagePopup _popup = new();
    private Icon? _icon;
    private bool _refreshing;

    private Strings S => Strings.For(_settings.Language);
    private Theme CurrentTheme => Theme.For(_settings.Theme);

    public TrayApp()
    {
        _tray = new NotifyIcon { Visible = true };
        _tray.MouseClick += OnTrayClick;
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                ShowMenu();
        };

        _popup.RefreshRequested += async (_, _) => await RefreshAsync();
        _popup.LanguageRequested += (_, lang) =>
        {
            _settings.Language = lang;
            _settings.Save();
            UpdateUi();
        };
        _popup.ThemeRequested += (_, mode) =>
        {
            _settings.Theme = mode;
            _settings.Save();
            UpdateUi();
        };

        _timer.Interval = (int)RefreshInterval.TotalMilliseconds;
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        UpdateUi();
        _ = RefreshAsync();
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        if (_popup.Visible)
            _popup.Hide();
        else if (!_popup.WasJustHidden()) // the click itself closed it via Deactivate
        {
            UpdateUi(); // picks up a Windows theme change
            _popup.ShowNearTray();
        }
    }

    private void ShowMenu()
    {
        _popup.Hide();
        var s = S;
        bool startup = StartupRegistry.IsEnabled();
        bool weekly = _settings.IconSource == IconSource.Weekly;
        var menu = new TrayMenu(
        [
            new(s.MenuRefresh, "\uE72C", () => _ = RefreshAsync(), Enabled: !_refreshing),
            null,
            new(s.MenuIconFiveHour, "\uE823", () => SetIconSource(IconSource.FiveHour), Selected: !weekly),
            new(s.MenuIconWeekly, "\uE787", () => SetIconSource(IconSource.Weekly), Selected: weekly),
            new(s.MenuIconPercent, "%", ToggleIconPercent, Toggle: _settings.IconShowPercent),
            null,
            new(s.MenuStartWithWindows, "\uE768", () => StartupRegistry.SetEnabled(!startup), Toggle: startup),
            new(s.MenuExit, "\uE7E8", ExitThread),
        ], CurrentTheme, _settings.Language);
        menu.ShowAt(Cursor.Position);
    }

    private void SetIconSource(IconSource source)
    {
        _settings.IconSource = source;
        _settings.Save();
        UpdateUi();
    }

    private void ToggleIconPercent()
    {
        _settings.IconShowPercent = !_settings.IconShowPercent;
        _settings.Save();
        UpdateUi();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;
        UpdateUi();

        try
        {
            FetchResult result;
            try
            {
                result = await _client.FetchAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Fetch failed", ex);
                result = FetchResult.Fail(FetchStatus.NetworkError);
            }

            _state.Apply(result, DateTimeOffset.UtcNow);
            if (!result.IsSuccess)
                Logger.Write($"Fetch result: {result.Status} {result.HttpStatusCode}");
        }
        finally
        {
            _refreshing = false;
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        try
        {
            var s = S;
            var snap = _state.Snapshot;
            bool stale = snap is null || _state.IsStale;
            var window = _settings.IconSource == IconSource.Weekly ? snap?.Data.SevenDay : snap?.Data.FiveHour;
            var newIcon = IconRenderer.Render(window?.Utilization, stale, _settings.IconShowPercent);
            _tray.Icon = newIcon;
            _icon?.Dispose();
            _icon = newIcon;
            _tray.Text = TooltipText(s);

            _popup.SetData(_state, s, _refreshing, CurrentTheme);
        }
        catch (Exception ex)
        {
            Logger.Error("UI update failed", ex);
        }
    }

    private string TooltipText(Strings s)
    {
        string text;
        if (_state.Snapshot is { } snap)
        {
            text = ResetFormatter.Tooltip(snap.Data);
            if (_state.IsStale)
                text += s.Language == Language.Thai ? " (ข้อมูลเก่า)" : " (stale)";
        }
        else if (_state.LastError is { } err)
            text = "Claude Usage: " + s.Error(err.Status, err.HttpStatusCode);
        else
            text = "Claude Usage: " + s.Loading;

        return text.Length <= 127 ? text : text[..126] + "…"; // NotifyIcon limit
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
            _popup.Dispose();
            _icon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
