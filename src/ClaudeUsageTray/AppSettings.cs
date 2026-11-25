using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeUsageTray.Core;
using Microsoft.Win32;

namespace ClaudeUsageTray;

/// <summary>User settings saved in %LOCALAPPDATA%\ClaudeUsageTray\settings.json.</summary>
internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string FilePath => Path.Combine(Logger.Directory, "settings.json");

    /// <summary>System until the user picks light or dark in the popup.</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public Language Language { get; set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th" ? Language.Thai : Language.English;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            Logger.Error("Cannot load settings", ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Logger.Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Logger.Error("Cannot save settings", ex);
        }
    }
}

/// <summary>Start with Windows via HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
internal static class StartupRegistry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeUsageTray";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception ex)
        {
            Logger.Error("Cannot read startup registry", ex);
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Logger.Error("Cannot write startup registry", ex);
        }
    }
}
