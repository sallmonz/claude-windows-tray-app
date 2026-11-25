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
    /// <summary>One menu row. Use null in the item list for a separator.</summary>
    public sealed record Item(string Text, string Glyph, Action OnClick, bool Enabled = true);

    private const float RowHeight = 36, SeparatorHeight = 9, VPad = 4, HInset = 4;
    private const float IconX = 16, TextX = 46, MinWidth = 200;

    private readonly IReadOnlyList<Item?> _items;
    private readonly Theme _theme;
    private readonly Font _font;
    private readonly Font _icon;
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
            _width = Math.Max(MinWidth, TextX + textW + 32);
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
                g.DrawString(item.Glyph, _icon, brush, new RectangleF(IconX - 4, row.Top, 24, row.Height), center);
            g.DrawString(item.Text, _font, brush, new RectangleF(TextX, row.Top, _width - TextX - 12, row.Height), vcenter);
        }

        g.ResetTransform();
        using var border = new Pen(_theme.MenuBorder, 1);
        g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
