using Microsoft.Win32;

namespace ClaudeUsageTray;

/// <summary>Theme the user picked. System follows the Windows app theme.</summary>
internal enum ThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>Colors for the popup and the right-click menu.</summary>
internal sealed record Theme(
    bool IsDark,
    Color Background, Color Text, Color SubText, Color Track, Color Divider,
    Color Border, Color Chip, Color ChipSelected, Color Warning,
    Color MenuBackground, Color MenuHover, Color MenuBorder)
{
    public static readonly Theme Dark = new(true,
        Hex(0x202020), Hex(0xFFFFFF), Hex(0xA0A0A0), Hex(0x3A3A3A), Hex(0x333333),
        Hex(0x3C3C3C), Hex(0x2D2D2D), Hex(0x4A4A4A), Hex(0xE3A008),
        Hex(0x2C2C2C), Hex(0x3A3A3A), Hex(0x404040));

    public static readonly Theme Light = new(false,
        Hex(0xF9F9F9), Hex(0x1A1A1A), Hex(0x606060), Hex(0xE1E1E1), Hex(0xE5E5E5),
        Hex(0xD0D0D0), Hex(0xECECEC), Hex(0xFFFFFF), Hex(0x9A6700),
        Hex(0xF9F9F9), Hex(0xEAEAEA), Hex(0xDADADA));

    private static Color Hex(int rgb) => Color.FromArgb(255, Color.FromArgb(rgb));

    public static Theme For(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => Light,
        ThemeMode.Dark => Dark,
        _ => SystemIsDark() ? Dark : Light,
    };

    private static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10 (same code points).</summary>
    public static string IconFont { get; } = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    /// <summary>Segoe UI has no Thai glyphs; Leelawadee UI ships with Windows and covers Thai and Latin.</summary>
    public static string UiFont(Core.Language language) =>
        language == Core.Language.Thai ? "Leelawadee UI" : "Segoe UI";

    private static bool FontExists(string name)
    {
        try
        {
            using var family = new FontFamily(name);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
