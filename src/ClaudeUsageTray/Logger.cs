namespace ClaudeUsageTray;

/// <summary>Writes to %LOCALAPPDATA%\ClaudeUsageTray\log.txt. Never throws.</summary>
internal static class Logger
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeUsageTray");

    private static string LogPath => Path.Combine(Directory, "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var file = new FileInfo(LogPath);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(LogPath, Path.Combine(Directory, "log.old.txt"), overwrite: true);

                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    public static void Error(string context, Exception? ex) => Write($"ERROR {context}: {ex}");
}
