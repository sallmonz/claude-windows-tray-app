using System.Runtime.InteropServices;

namespace ClaudeUsageTray;

/// <summary>
/// Right-click menu built with the real Win32 popup menu, so it looks like other tray menus
/// (rounded corners on Windows 11, system font, light/dark theme).
/// </summary>
internal sealed partial class NativeTrayMenu : IDisposable
{
    /// <summary>One menu entry. Use null in the item list for a separator.</summary>
    public sealed record Item(string Text, Action OnClick, bool Enabled = true, bool Checked = false);

    private readonly NativeWindow _owner = new();

    public NativeTrayMenu()
    {
        _owner.CreateHandle(new CreateParams { Caption = "ClaudeUsageTray.Menu" });
        DarkMenus.Allow(_owner.Handle);
    }

    public void Show(IReadOnlyList<Item?> items, Point screenPoint)
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is not { } item)
                {
                    AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                    continue;
                }
                uint flags = MF_STRING | (item.Enabled ? 0 : MF_GRAYED) | (item.Checked ? MF_CHECKED : 0);
                AppendMenuW(menu, flags, (UIntPtr)(i + 1), item.Text);
            }

            DarkMenus.Refresh();

            // Required for tray menus: without this the menu does not close when you click elsewhere.
            SetForegroundWindow(_owner.Handle);
            uint align = SystemInformation.RightAlignedMenus ? TPM_RIGHTALIGN : TPM_LEFTALIGN;
            uint cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | align,
                screenPoint.X, screenPoint.Y, _owner.Handle, IntPtr.Zero);
            PostMessageW(_owner.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (cmd > 0 && cmd <= items.Count)
                items[(int)cmd - 1]?.OnClick();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose() => _owner.DestroyHandle();

    private const uint MF_STRING = 0x0, MF_GRAYED = 0x1, MF_CHECKED = 0x8, MF_SEPARATOR = 0x800;
    private const uint TPM_LEFTALIGN = 0x0, TPM_RIGHTALIGN = 0x8, TPM_BOTTOMALIGN = 0x20;
    private const uint TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100;
    private const uint WM_NULL = 0x0;

    [LibraryImport("user32.dll")]
    private static partial IntPtr CreatePopupMenu();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenuW(IntPtr hMenu, uint flags, UIntPtr id, string? text);

    [LibraryImport("user32.dll")]
    private static partial uint TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(IntPtr hMenu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Lets Win32 menus follow the Windows dark app theme. Windows has no public API for this,
    /// so it uses the uxtheme functions that Explorer and many tray apps use (Windows 10 1903+).
    /// If they are missing, menus simply stay light.
    /// </summary>
    private static class DarkMenus
    {
        private const int AllowDark = 1;

        private static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362);

        public static void Allow(IntPtr hwnd)
        {
            if (!Supported) return;
            try
            {
                SetPreferredAppMode(AllowDark);
                AllowDarkModeForWindow(hwnd, true);
                FlushMenuThemes();
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
            {
                Logger.Write("Dark menus not available: " + ex.Message);
            }
        }

        /// <summary>Picks up a theme change made while the app is running.</summary>
        public static void Refresh()
        {
            if (!Supported) return;
            try { FlushMenuThemes(); }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { }
        }

        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#133")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowDarkModeForWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        private static extern void FlushMenuThemes();
    }
}
