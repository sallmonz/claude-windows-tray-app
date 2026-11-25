using System.Runtime.InteropServices;

namespace ClaudeUsageTray;

internal static partial class Native
{
    public const int WS_EX_TOOLWINDOW = 0x80; // keep out of Alt+Tab
    public const int CS_DROPSHADOW = 0x20000;

    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    /// <summary>Rounded corners on Windows 11. Does nothing on Windows 10.</summary>
    public static void SetCornerPreference(IntPtr hwnd, int preference) =>
        _ = DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref preference, sizeof(int));

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hWnd);
}
