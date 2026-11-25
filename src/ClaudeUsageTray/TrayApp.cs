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
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _refreshItem = new();
    private readonly ToolStripMenuItem _startupItem = new() { CheckOnClick = false };
    private readonly ToolStripMenuItem _exitItem = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly UsagePopup _popup = new();
    private Icon? _icon;
    private bool _refreshing;

    private Strings S => Strings.For(_settings.Language);

    public TrayApp()
    {
        _refreshItem.Click += async (_, _) => await RefreshAsync();
        _startupItem.Click += (_, _) => StartupRegistry.SetEnabled(!StartupRegistry.IsEnabled());
        _exitItem.Click += (_, _) => ExitThread();
        _menu.Items.AddRange([_refreshItem, _startupItem, new ToolStripSeparator(), _exitItem]);
        _menu.Opening += (_, _) => _startupItem.Checked = StartupRegistry.IsEnabled();

        _tray = new NotifyIcon { ContextMenuStrip = _menu, Visible = true };
        _tray.MouseClick += OnTrayClick;

        _popup.RefreshRequested += async (_, _) => await RefreshAsync();
        _popup.LanguageRequested += (_, lang) =>
        {
            _settings.Language = lang;
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
            _popup.ShowNearTray();
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
            _refreshItem.Text = s.MenuRefresh;
            _refreshItem.Enabled = !_refreshing;
            _startupItem.Text = s.MenuStartWithWindows;
            _exitItem.Text = s.MenuExit;

            var snap = _state.Snapshot;
            bool stale = snap is null || _state.IsStale;
            var newIcon = IconRenderer.Render(snap?.Data.FiveHour?.Utilization, stale);
            _tray.Icon = newIcon;
            _icon?.Dispose();
            _icon = newIcon;
            _tray.Text = TooltipText(s);

            _popup.SetData(_state, s, _refreshing);
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
            _menu.Dispose();
            _popup.Dispose();
            _icon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
