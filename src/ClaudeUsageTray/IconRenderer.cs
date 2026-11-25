using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ClaudeUsageTray.Core;

namespace ClaudeUsageTray;

/// <summary>Shared colors for the icon and the popup.</summary>
internal static class Palette
{
    public static readonly Color Green = Color.FromArgb(0x3F, 0xB9, 0x50);
    public static readonly Color Orange = Color.FromArgb(0xF0, 0x88, 0x3E);
    public static readonly Color Red = Color.FromArgb(0xF8, 0x51, 0x49);
    public static readonly Color Gray = Color.FromArgb(0x9E, 0x9E, 0x9E);

    public static Color ForLevel(UsageLevel level) => level switch
    {
        UsageLevel.Low => Green,
        UsageLevel.Medium => Orange,
        _ => Red,
    };

    public static Color ForUsage(double? percent, bool stale) =>
        stale || percent is null ? Gray : ForLevel(ResetFormatter.Level(percent.Value));
}

/// <summary>Draws the 5-hour percent onto a small tray icon with GDI+.</summary>
internal static partial class IconRenderer
{
    public static Icon Render(double? percent, bool stale)
    {
        int size = SystemInformation.SmallIconSize.Width; // 16 at 100% scale
        int? rounded = percent is { } p ? (int)Math.Round(p, MidpointRounding.AwayFromZero) : null;
        string text = rounded switch
        {
            null => "–",
            >= 100 => "!!",
            _ => rounded.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using (var bg = new SolidBrush(Palette.ForUsage(percent, stale)))
            using (var shape = RoundedRect(new RectangleF(0, 0, size, size), size / 4f))
                g.FillPath(bg, shape);

            using var family = new FontFamily("Segoe UI");
            using var path = new GraphicsPath();
            float em = size * (text.Length >= 2 ? 0.78f : 0.9f);
            path.AddString(text, family, (int)FontStyle.Bold, em, PointF.Empty, StringFormat.GenericTypographic);

            // Fit inside the icon with a 1px margin, then center by the real glyph bounds.
            var b = path.GetBounds();
            float scale = Math.Min(1f, Math.Min((size - 2) / b.Width, (size - 2) / b.Height));
            using var m = new Matrix();
            m.Translate(size / 2f, size / 2f);
            m.Scale(scale, scale);
            m.Translate(-(b.Left + b.Width / 2f), -(b.Top + b.Height / 2f));
            path.Transform(m);
            g.FillPath(Brushes.White, path);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);
}
