using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using ClaudeUsageTray.Core;

namespace ClaudeUsageTray;

/// <summary>
/// Borderless popup above the tray. Everything is painted by hand in logical 96-DPI units
/// and scaled to the monitor DPI.
/// </summary>
internal sealed class UsagePopup : Form
{
    // Logical layout (96 DPI).
    private const float W = 320, Pad = 16;
    private const float RingSize = 96, RingStroke = 8;

    private static readonly RectangleF RefreshRect = new(W - Pad - 24, 12, 24, 24);
    private static readonly RectangleF LangRect = new(RefreshRect.Left - 8 - 64, 13, 64, 22);
    private static readonly RectangleF ThemeRect = new(LangRect.Left - 8 - 56, 13, 56, 22);
    private static RectangleF SunRect => new(ThemeRect.Left, ThemeRect.Top, ThemeRect.Width / 2, ThemeRect.Height);
    private static RectangleF MoonRect => new(ThemeRect.Left + ThemeRect.Width / 2, ThemeRect.Top, ThemeRect.Width / 2, ThemeRect.Height);
    private static RectangleF EnRect => new(LangRect.Left, LangRect.Top, LangRect.Width / 2, LangRect.Height);
    private static RectangleF ThRect => new(LangRect.Left + LangRect.Width / 2, LangRect.Top, LangRect.Width / 2, LangRect.Height);

    private enum Hit { None, Refresh, En, Th, Sun, Moon }

    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 30_000 };
    private UsageState _state = new();
    private Strings _s = Strings.English;
    private bool _refreshing;
    private Theme _theme = Theme.Light;
    private PopupFonts _fonts = new(Language.English);
    private Hit _hover;
    private long _hiddenAt;
    private float _height = 280;

    public event EventHandler? RefreshRequested;
    public event EventHandler<Language>? LanguageRequested;
    public event EventHandler<ThemeMode>? ThemeRequested;

    public UsagePopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        Text = "Claude Usage";
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        _tick.Tick += (_, _) => Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            cp.ClassStyle |= Native.CS_DROPSHADOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => false;

    private float DpiScale => DeviceDpi / 96f;

    /// <summary>True if the popup closed a moment ago (the same tray click that should not reopen it).</summary>
    public bool WasJustHidden() => Environment.TickCount64 - _hiddenAt < 300;

    public void SetData(UsageState state, Strings strings, bool refreshing, Theme theme)
    {
        _state = state;
        _theme = theme;
        BackColor = theme.Background;
        _refreshing = refreshing;
        if (_s != strings)
        {
            _s = strings;
            _fonts.Dispose();
            _fonts = new PopupFonts(strings.Language);
        }
        UpdateLayoutAndPosition();
        Invalidate();
    }

    public void ShowNearTray()
    {
        UpdateLayoutAndPosition();
        Show();
        Activate();
        Native.SetForegroundWindow(Handle);
        _tick.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Rounded corners on Windows 11. Ignored on Windows 10.
        Native.SetCornerPreference(Handle, Native.DWMWCP_ROUND);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        HidePopup();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            HidePopup();
    }

    private void HidePopup()
    {
        if (!Visible) return;
        _hiddenAt = Environment.TickCount64;
        _tick.Stop();
        Hide();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateLayoutAndPosition();
    }

    private void UpdateLayoutAndPosition()
    {
        using (var g = CreateGraphics())
            _height = MeasureHeight(g);

        var size = new Size((int)Math.Ceiling(W * DpiScale), (int)Math.Ceiling(_height * DpiScale));
        var area = (Screen.PrimaryScreen ?? Screen.FromControl(this)).WorkingArea;
        int margin = (int)(12 * DpiScale);
        Bounds = new Rectangle(area.Right - size.Width - margin, area.Bottom - size.Height - margin,
            size.Width, size.Height);
    }

    // ---------- Mouse ----------

    private Hit HitTest(Point p)
    {
        var pt = new PointF(p.X / DpiScale, p.Y / DpiScale);
        if (RefreshRect.Contains(pt)) return Hit.Refresh;
        if (EnRect.Contains(pt)) return Hit.En;
        if (ThRect.Contains(pt)) return Hit.Th;
        if (SunRect.Contains(pt)) return Hit.Sun;
        if (MoonRect.Contains(pt)) return Hit.Moon;
        return Hit.None;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (hit == _hover) return;
        _hover = hit;
        Cursor = hit == Hit.None ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = Hit.None;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        switch (HitTest(e.Location))
        {
            case Hit.Refresh when !_refreshing:
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Hit.En when _s.Language != Language.English:
                LanguageRequested?.Invoke(this, Language.English);
                break;
            case Hit.Th when _s.Language != Language.Thai:
                LanguageRequested?.Invoke(this, Language.Thai);
                break;
            case Hit.Sun when _theme.IsDark:
                ThemeRequested?.Invoke(this, ThemeMode.Light);
                break;
            case Hit.Moon when !_theme.IsDark:
                ThemeRequested?.Invoke(this, ThemeMode.Dark);
                break;
        }
    }

    // ---------- Layout & paint ----------

    private enum Mode { Loading, Error, Data }

    private Mode CurrentMode =>
        _state.Snapshot is not null ? Mode.Data :
        _state.LastError is not null ? Mode.Error : Mode.Loading;

    private const float HeaderBottom = 44;
    private const float ContentWidth = W - Pad * 2;

    private float MeasureHeight(Graphics g)
    {
        switch (CurrentMode)
        {
            case Mode.Loading:
                return HeaderBottom + 100;
            case Mode.Error:
                var msg = ErrorText();
                float eh = HeaderBottom + 12 + 32 + 12 + Measure(g, msg, _fonts.Strong, ContentWidth).Height + 24;
                if (ErrorDetail() is { } detail)
                    eh += Measure(g, detail, _fonts.Footer, ContentWidth).Height + 6;
                return eh;
            default:
                float y = DataFooterTop + 8;
                if (StaleText() is { } stale)
                    y += Measure(g, stale, _fonts.Footer, ContentWidth).Height + 2;
                return y + _fonts.Footer.Height + 12;
        }
    }

    // Fixed positions for the data view.
    private const float RingTop = HeaderBottom + 6;
    private const float Divider1 = RingTop + RingSize + 16;
    private const float WeeklyTop = Divider1 + 12;
    private const float BarTop = WeeklyTop + 34;
    private const float DataFooterTop = BarTop + 6 + 30;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.Background);
        g.ScaleTransform(DpiScale, DpiScale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        DrawHeader(g);
        switch (CurrentMode)
        {
            case Mode.Loading:
                DrawCentered(g, _s.Loading, _fonts.Label, _theme.SubText, HeaderBottom + 38);
                break;
            case Mode.Error:
                DrawError(g);
                break;
            default:
                DrawData(g, _state.Snapshot!);
                break;
        }

        // Thin border so the popup stays visible on any wallpaper.
        using var border = new Pen(_theme.Border, 1);
        g.ResetTransform();
        g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private void DrawHeader(Graphics g)
    {
        DrawText(g, "Claude Usage", _fonts.Title, _theme.Text, Pad, 16);

        // EN | TH segmented switch.
        using (var chip = IconRenderer.RoundedRect(LangRect, 5))
        using (var chipBrush = new SolidBrush(_theme.Chip))
            g.FillPath(chipBrush, chip);
        DrawSegment(g, EnRect, "EN", _s.Language == Language.English, _hover == Hit.En);
        DrawSegment(g, ThRect, "TH", _s.Language == Language.Thai, _hover == Hit.Th);

        // Sun | Moon theme switch.
        using (var chip = IconRenderer.RoundedRect(ThemeRect, 5))
        using (var chipBrush = new SolidBrush(_theme.Chip))
            g.FillPath(chipBrush, chip);
        DrawSegment(g, SunRect, "\uE706", !_theme.IsDark, _hover == Hit.Sun, _fonts.SmallGlyph);
        DrawSegment(g, MoonRect, "\uE708", _theme.IsDark, _hover == Hit.Moon, _fonts.SmallGlyph);

        // Refresh button.
        if (_hover == Hit.Refresh && !_refreshing)
        {
            using var hover = IconRenderer.RoundedRect(RefreshRect, 5);
            using var hb = new SolidBrush(_theme.Chip);
            g.FillPath(hb, hover);
        }
        var glyphColor = _refreshing ? Color.FromArgb(110, _theme.SubText) : _theme.SubText;
        DrawCenteredIn(g, "", _fonts.Glyph, glyphColor, RefreshRect);
    }

    private void DrawSegment(Graphics g, RectangleF r, string text, bool selected, bool hover, Font? font = null)
    {
        var inner = RectangleF.Inflate(r, -2, -2);
        if (selected || hover)
        {
            using var path = IconRenderer.RoundedRect(inner, 4);
            using var b = new SolidBrush(selected ? _theme.ChipSelected : Color.FromArgb(60, _theme.ChipSelected));
            g.FillPath(b, path);
        }
        DrawCenteredIn(g, text, font ?? (selected ? _fonts.SegmentOn : _fonts.Segment),
            selected ? _theme.Text : _theme.SubText, inner);
    }

    private void DrawData(Graphics g, UsageSnapshot snap)
    {
        var now = DateTimeOffset.UtcNow;
        bool stale = _state.IsStale;
        var five = snap.Data.FiveHour;
        var week = snap.Data.SevenDay;

        // --- 5-hour ring ---
        var ring = new RectangleF(Pad + RingStroke / 2, RingTop + RingStroke / 2,
            RingSize - RingStroke, RingSize - RingStroke);
        using (var track = new Pen(_theme.Track, RingStroke))
            g.DrawEllipse(track, ring);
        if (five?.Utilization is { } u5 && u5 > 0)
        {
            using var arc = new Pen(Palette.ForUsage(u5, stale), RingStroke)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float sweep = (float)(Math.Min(u5, 100) / 100 * 360);
            if (sweep >= 360) g.DrawEllipse(arc, ring);
            else g.DrawArc(arc, ring, -90, sweep);
        }

        float cx = Pad + RingSize / 2, cy = RingTop + RingSize / 2;
        var p5 = ResetFormatter.Percent(five);
        DrawBigPercent(g, p5, _fonts.RingNumber, _fonts.RingPercent, cx, cy, center: true);

        // Text next to the ring, centered on the ring vertically.
        float tx = Pad + RingSize + 16, tw = W - Pad - tx;
        var r5 = ResetFormatter.Format(five?.ResetsAt, now, TimeZoneInfo.Local, _s, includeDay: false);
        var labelH = _fonts.Label.GetHeight(g);
        var strongH = Measure(g, r5.Countdown, _fonts.Strong, tw).Height;
        var atText = r5.At is null ? null : _s.At(r5.At);
        var atH = atText is null ? 0 : _fonts.Small.GetHeight(g) + 2;
        float blockH = labelH + 4 + strongH + atH;
        float ty = cy - blockH / 2;
        DrawText(g, _s.FiveHourLabel, _fonts.Label, _theme.SubText, tx, ty);
        ty += labelH + 4;
        DrawWrapped(g, r5.Countdown, _fonts.Strong, _theme.Text, new RectangleF(tx, ty, tw, strongH));
        ty += strongH + 2;
        if (atText is not null)
            DrawText(g, atText, _fonts.Small, _theme.SubText, tx, ty);

        DrawDivider(g, Divider1);

        // --- Weekly bar ---
        float baseline = WeeklyTop + 26;
        DrawAtBaseline(g, _s.WeeklyLabel, _fonts.Label, _theme.SubText, Pad, baseline);
        DrawBigPercent(g, ResetFormatter.Percent(week), _fonts.WeekNumber, _fonts.WeekPercent,
            W - Pad, baseline, center: false);

        var bar = new RectangleF(Pad, BarTop, ContentWidth, 6);
        using (var trackPath = IconRenderer.RoundedRect(bar, 3))
        using (var trackBrush = new SolidBrush(_theme.Track))
            g.FillPath(trackBrush, trackPath);
        if (week?.Utilization is { } u7 && u7 > 0)
        {
            float fw = Math.Max(bar.Height, (float)(Math.Min(u7, 100) / 100 * bar.Width));
            using var fill = IconRenderer.RoundedRect(new RectangleF(bar.X, bar.Y, fw, bar.Height), 3);
            using var fb = new SolidBrush(Palette.ForUsage(u7, stale));
            g.FillPath(fb, fill);
        }
        var r7 = ResetFormatter.Format(week?.ResetsAt, now, TimeZoneInfo.Local, _s, includeDay: true);
        DrawText(g, r7.Combined, _fonts.Small, _theme.SubText, Pad, BarTop + 12);

        DrawDivider(g, DataFooterTop);

        // --- Footer ---
        float fy = DataFooterTop + 8;
        if (StaleText() is { } staleText)
        {
            var h = Measure(g, staleText, _fonts.Footer, ContentWidth).Height;
            DrawWrapped(g, staleText, _fonts.Footer, _theme.Warning, new RectangleF(Pad, fy, ContentWidth, h));
            fy += h + 2;
        }
        DrawText(g, $"{_s.LastUpdated} {FormatFetched(snap.FetchedAt)}", _fonts.Footer, _theme.SubText, Pad, fy);
    }

    private void DrawError(Graphics g)
    {
        float y = HeaderBottom + 12;
        DrawCenteredIn(g, "", _fonts.BigGlyph, _theme.Warning, new RectangleF(0, y, W, 32));
        y += 32 + 12;
        var msg = ErrorText();
        var h = Measure(g, msg, _fonts.Strong, ContentWidth).Height;
        using var sf = new StringFormat { Alignment = StringAlignment.Center };
        using var b = new SolidBrush(_theme.Text);
        g.DrawString(msg, _fonts.Strong, b, new RectangleF(Pad, y, ContentWidth, h + 2), sf);

        if (ErrorDetail() is { } detail)
        {
            y += h + 6;
            var dh = Measure(g, detail, _fonts.Footer, ContentWidth).Height;
            using var sub = new SolidBrush(_theme.SubText);
            g.DrawString(detail, _fonts.Footer, sub, new RectangleF(Pad, y, ContentWidth, dh + 2), sf);
        }
    }

    /// <summary>Shows where the app looked for the token, so login problems are easy to check.</summary>
    private static string? ErrorDetailFor(FetchStatus status) =>
        status == FetchStatus.NoCredentials ? UsageClient.DefaultCredentialsPath : null;

    private string? ErrorDetail() => _state.LastError is { } err ? ErrorDetailFor(err.Status) : null;

    private string ErrorText() =>
        _state.LastError is { } err ? _s.Error(err.Status, err.HttpStatusCode) : "";

    private string? StaleText()
    {
        if (!_state.IsStale || _state.LastError is not { } err || _state.LastErrorAt is not { } at)
            return null;
        var time = at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        return _s.Stale(_s.Error(err.Status, err.HttpStatusCode), time);
    }

    private static string FormatFetched(DateTimeOffset fetched)
    {
        var local = fetched.ToLocalTime();
        var format = local.Date == DateTime.Today ? "HH:mm:ss" : "dd/MM HH:mm";
        return local.ToString(format, CultureInfo.InvariantCulture);
    }

    // ---------- Drawing helpers ----------

    /// <summary>Big number with a small "%" sharing its baseline.</summary>
    private void DrawBigPercent(Graphics g, int? value, Font numFont, Font pctFont, float x, float y, bool center)
    {
        string num = value?.ToString(CultureInfo.InvariantCulture) ?? "—";
        float numW = MeasureTight(g, num, numFont);
        float pctW = value is null ? 0 : MeasureTight(g, "%", pctFont) + 2;
        float total = numW + pctW;

        float left, baseline;
        if (center)
        {
            // Center the digits on (x, y) using their cap height.
            left = x - total / 2;
            baseline = y + CapHeight(numFont) / 2;
        }
        else
        {
            left = x - total; // right aligned
            baseline = y;
        }

        DrawAtBaseline(g, num, numFont, _theme.Text, left, baseline);
        if (value is not null)
            DrawAtBaseline(g, "%", pctFont, _theme.SubText, left + numW + 2, baseline);
    }

    private static float CapHeight(Font f) => f.Size * 0.7f; // Segoe UI digits are ~0.7 em tall

    private static float Ascent(Font f) =>
        f.Size * f.FontFamily.GetCellAscent(f.Style) / f.FontFamily.GetEmHeight(f.Style);

    private static void DrawAtBaseline(Graphics g, string text, Font font, Color color, float x, float baseline)
    {
        using var b = new SolidBrush(color);
        g.DrawString(text, font, b, x, baseline - Ascent(font), StringFormat.GenericTypographic);
    }

    private static float MeasureTight(Graphics g, string text, Font font) =>
        g.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width;

    private static SizeF Measure(Graphics g, string text, Font font, float width) =>
        g.MeasureString(text, font, (int)Math.Ceiling(width));

    private static void DrawText(Graphics g, string text, Font font, Color color, float x, float y)
    {
        using var b = new SolidBrush(color);
        g.DrawString(text, font, b, x, y);
    }

    private static void DrawWrapped(Graphics g, string text, Font font, Color color, RectangleF r)
    {
        using var b = new SolidBrush(color);
        g.DrawString(text, font, b, new RectangleF(r.X, r.Y, r.Width, r.Height + 2));
    }

    private static void DrawCentered(Graphics g, string text, Font font, Color color, float y) =>
        DrawCenteredIn(g, text, font, color, new RectangleF(0, y, W, font.Height + 4));

    private static void DrawCenteredIn(Graphics g, string text, Font font, Color color, RectangleF r)
    {
        using var b = new SolidBrush(color);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, b, r, sf);
    }

    private void DrawDivider(Graphics g, float y)
    {
        using var pen = new Pen(_theme.Divider, 1);
        g.DrawLine(pen, Pad, y, W - Pad, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tick.Dispose();
            _fonts.Dispose();
        }
        base.Dispose(disposing);
    }

    // ---------- Fonts ----------

    private sealed class PopupFonts : IDisposable
    {
        public Font Title { get; }
        public Font Label { get; }
        public Font Strong { get; }
        public Font Small { get; }
        public Font Footer { get; }
        public Font Segment { get; }
        public Font SegmentOn { get; }
        public Font RingNumber { get; }
        public Font RingPercent { get; }
        public Font WeekNumber { get; }
        public Font WeekPercent { get; }
        public Font Glyph { get; }
        public Font BigGlyph { get; }
        public Font SmallGlyph { get; }

        public PopupFonts(Language language)
        {
            // Segoe UI has no Thai glyphs; Leelawadee UI ships with Windows and covers Thai and Latin.
            bool th = language == Language.Thai;
            string ui = Theme.UiFont(language);
            Font Ui(float px, bool bold = false) => th
                ? new Font(ui, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel)
                : new Font(bold ? "Segoe UI Semibold" : ui, px, FontStyle.Regular, GraphicsUnit.Pixel);
            Font Px(string family, float px) => new(family, px, FontStyle.Regular, GraphicsUnit.Pixel);

            Title = Px("Segoe UI Semibold", 13); // "Claude Usage" is Latin text: same font in both languages
            Label = Ui(12);
            Strong = Ui(14, bold: true);
            Small = Ui(12);
            Footer = Ui(11);
            Segment = Px("Segoe UI", 11);
            SegmentOn = Px("Segoe UI Semibold", 11);
            RingNumber = Px("Segoe UI Semibold", 36);
            RingPercent = Px("Segoe UI", 14);
            WeekNumber = Px("Segoe UI Semibold", 26);
            WeekPercent = Px("Segoe UI", 13);
            Glyph = Px(Theme.IconFont, 14);
            BigGlyph = Px(Theme.IconFont, 28);
            SmallGlyph = Px(Theme.IconFont, 12);
        }

        public void Dispose()
        {
            foreach (var f in new[] { Title, Label, Strong, Small, Footer, Segment, SegmentOn,
                         RingNumber, RingPercent, WeekNumber, WeekPercent, Glyph, BigGlyph, SmallGlyph })
                f.Dispose();
        }
    }
}
