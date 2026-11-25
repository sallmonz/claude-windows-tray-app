using System.Drawing.Drawing2D;
using System.Drawing.Text;
using ClaudeUsageTray.Core;

namespace ClaudeUsageTray;

/// <summary>
/// Right-click menu drawn in the Windows 11 style (rounded corners, roomy rows, icons, rounded hover).
/// Windows does not offer this style to Win32 menus, so it is painted by hand like the popup.
/// A new instance is created for each open and disposed when it closes.
/// </summary>
internal sealed class TrayMenu : Form
{
    /// <summary>
    /// One menu row. Use null in the item list for a separator.
    /// Toggle draws an on/off switch on the right; Selected draws a check mark on the right.
    /// </summary>
    public sealed record Item(string Text, string Glyph, Action OnClick, bool Enabled = true,
        bool? Toggle = null, bool Selected = false);

    private const float RowHeight = 36, SeparatorHeight = 9, VPad = 4, HInset = 4;
    private const float IconX = 16, TextX = 46, MinWidth = 200, RightColumn = 56;

    private readonly IReadOnlyList<Item?> _items;
    private readonly Theme _theme;
    private readonly Font _font;
    private readonly Font _icon;
    private readonly Font _textIcon;
    private readonly float _width;
    private readonly float _height;
    private int _hover = -1;
    private bool _closed;

    public TrayMenu(IReadOnlyList<Item?> items, Theme theme, Language language)
    {
        _items = items;
        _theme = theme;
        _font = new Font(Theme.UiFont(language), 14, FontStyle.Regular, GraphicsUnit.Pixel);
        _icon = new Font(Theme.IconFont, 16, FontStyle.Regular, GraphicsUnit.Pixel);
        _textIcon = new Font("Segoe UI Semibold", 15, FontStyle.Regular, GraphicsUnit.Pixel);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = theme.MenuBackground;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        using (var g = CreateGraphics())
        {
            float textW = items.OfType<Item>().Select(i => g.MeasureString(i.Text, _font).Width).DefaultIfEmpty(0).Max();
            bool hasRight = items.OfType<Item>().Any(i => i.Toggle is not null || i.Selected);
            _width = Math.Max(MinWidth, TextX + textW + (hasRight ? RightColumn + 16 : 32));
        }
        _height = VPad * 2 + items.Sum(i => i is null ? SeparatorHeight : RowHeight);
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

    private float DpiScale => DeviceDpi / 96f;

    /// <summary>Opens above the taskbar, centered on the click point, like the Windows 11 tray menus.</summary>
    public void ShowAt(Point anchor)
    {
        var area = Screen.FromPoint(anchor).WorkingArea;
        var size = new Size((int)Math.Ceiling(_width * DpiScale), (int)Math.Ceiling(_height * DpiScale));
        int gap = (int)(12 * DpiScale);
        int x = Math.Clamp(anchor.X - size.Width / 2, area.Left + gap, area.Right - size.Width - gap);
        int y = area.Bottom - size.Height - gap;
        Bounds = new Rectangle(x, y, size.Width, size.Height);
        Show();
        Activate();
        Native.SetForegroundWindow(Handle);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetCornerPreference(Handle, Native.DWMWCP_ROUND);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        CloseOnce();
    }

    /// <summary>Close is reached from Deactivate, Esc and item clicks; only run it once.
    /// A modeless form disposes itself when closed.</summary>
    private void CloseOnce()
    {
        if (_closed) return;
        _closed = true;
        Close();
    }

    // ---------- Input ----------

    private RectangleF RowRect(int index)
    {
        float y = VPad;
        for (int i = 0; i < index; i++)
            y += _items[i] is null ? SeparatorHeight : RowHeight;
        return new RectangleF(0, y, _width, _items[index] is null ? SeparatorHeight : RowHeight);
    }

    private int RowAt(Point p)
    {
        var pt = new PointF(p.X / DpiScale, p.Y / DpiScale);
        for (int i = 0; i < _items.Count; i++)
            if (_items[i] is { Enabled: true } && RowRect(i).Contains(pt))
                return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(RowAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button is MouseButtons.Left or MouseButtons.Right)
            Invoke(RowAt(e.Location));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape:
                CloseOnce();
                return true;
            case Keys.Down:
                MoveHover(+1);
                return true;
            case Keys.Up:
                MoveHover(-1);
                return true;
            case Keys.Enter:
            case Keys.Space:
                Invoke(_hover);
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void MoveHover(int step)
    {
        int n = _items.Count, i = _hover;
        for (int tries = 0; tries < n; tries++)
        {
            i = ((i < 0 && step < 0 ? 0 : i) + step + n) % n;
            if (_items[i] is { Enabled: true })
            {
                SetHover(i);
                return;
            }
        }
    }

    private void SetHover(int index)
    {
        if (index == _hover) return;
        _hover = index;
        Invalidate();
    }

    private void Invoke(int index)
    {
        if (index < 0 || _items[index] is not { Enabled: true } item)
            return;
        CloseOnce();
        try
        {
            item.OnClick();
        }
        catch (Exception ex)
        {
            Logger.Error($"Menu action '{item.Text}' failed", ex);
        }
    }

    // ---------- Paint ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.MenuBackground);
        g.ScaleTransform(DpiScale, DpiScale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var vcenter = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < _items.Count; i++)
        {
            var row = RowRect(i);
            if (_items[i] is not { } item)
            {
                using var sep = new Pen(_theme.Divider, 1);
                float y = row.Top + row.Height / 2;
                g.DrawLine(sep, 0, y, _width, y);
                continue;
            }

            if (i == _hover)
            {
                var hl = new RectangleF(HInset, row.Top + 2, _width - HInset * 2, row.Height - 4);
                using var path = IconRenderer.RoundedRect(hl, 4);
                using var hb = new SolidBrush(_theme.MenuHover);
                g.FillPath(hb, path);
            }

            var color = item.Enabled ? _theme.Text : _theme.SubText;
            using var brush = new SolidBrush(color);
            if (item.Glyph.Length > 0)
            {
                // Private-use code points are icon-font glyphs; anything else (like "%") uses a text font.
                var iconFont = item.Glyph[0] >= '\uE000' ? _icon : _textIcon;
                g.DrawString(item.Glyph, iconFont, brush, new RectangleF(IconX - 4, row.Top, 24, row.Height), center);
            }
            g.DrawString(item.Text, _font, brush, new RectangleF(TextX, row.Top, _width - TextX - 12, row.Height), vcenter);

            if (item.Toggle is { } on)
                DrawSwitch(g, new RectangleF(_width - 16 - 40, row.Top + (row.Height - 20) / 2, 40, 20), on);
            else if (item.Selected)
                g.DrawString("\uE73E", _icon, brush, new RectangleF(_width - 16 - 24, row.Top, 24, row.Height), center);
        }

        g.ResetTransform();
        using var border = new Pen(_theme.MenuBorder, 1);
        g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    /// <summary>Windows 11 style toggle switch.</summary>
    private void DrawSwitch(Graphics g, RectangleF r, bool on)
    {
        using var pill = IconRenderer.RoundedRect(r, r.Height / 2);
        float knob = on ? 12 : 10;
        float kx = on ? r.Right - 4 - knob : r.Left + 5;
        var knobRect = new RectangleF(kx, r.Top + (r.Height - knob) / 2, knob, knob);
        if (on)
        {
            using var fill = new SolidBrush(AccentColor());
            g.FillPath(fill, pill);
            using var k = new SolidBrush(_theme.IsDark ? Color.Black : Color.White);
            g.FillEllipse(k, knobRect);
        }
        else
        {
            using var outline = new Pen(_theme.SubText, 1);
            g.DrawPath(outline, pill);
            using var k = new SolidBrush(_theme.SubText);
            g.FillEllipse(k, knobRect);
        }
    }

    /// <summary>The Windows accent color (DWM AccentColor is stored as ABGR), or Windows blue.</summary>
    private Color AccentColor()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var c = Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                return _theme.IsDark ? ControlPaint.Light(c, 0.6f) : c;
            }
        }
        catch
        {
            // fall through
        }
        return _theme.IsDark ? Color.FromArgb(0x4C, 0xC2, 0xFF) : Color.FromArgb(0x00, 0x67, 0xC0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _icon.Dispose();
            _textIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
