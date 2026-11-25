namespace ClaudeUsageTray.Core;

/// <summary>Countdown text such as "รีเซ็ตใน 2 ชม. 15 นาที" and the reset time "14:30" / "พฤ. 09:00".</summary>
public sealed record ResetText(string Countdown, string? At)
{
    /// <summary>One line, e.g. "Resets in 3 d 4 h (Thu 09:00)".</summary>
    public string Combined => At is null ? Countdown : $"{Countdown} ({At})";
}

/// <summary>Pure display helpers. Time zone and "now" are passed in so they are easy to test.</summary>
public static class ResetFormatter
{
    public static ResetText Format(DateTimeOffset? resetsAt, DateTimeOffset now, TimeZoneInfo zone,
        Strings s, bool includeDay)
    {
        if (resetsAt is not { } reset)
            return new ResetText(s.NoData, null);

        var remaining = reset - now;
        if (remaining <= TimeSpan.Zero)
            return new ResetText(s.ResetPassed, null);

        return new ResetText(s.ResetsIn(Duration(remaining, s)), FormatTime(reset, zone, s, includeDay));
    }

    public static string Duration(TimeSpan remaining, Strings s)
    {
        var totalMinutes = (long)Math.Floor(remaining.TotalMinutes);
        if (totalMinutes < 1)
            return s.LessThanMinute;

        int days = (int)(totalMinutes / (24 * 60));
        int hours = (int)(totalMinutes / 60 % 24);
        int minutes = (int)(totalMinutes % 60);

        if (days > 0) return s.Days(days, hours);
        if (hours > 0) return s.Hours(hours, minutes);
        return s.Minutes(minutes);
    }

    public static string FormatTime(DateTimeOffset time, TimeZoneInfo zone, Strings s, bool includeDay)
    {
        var local = TimeZoneInfo.ConvertTime(time, zone);
        var hhmm = local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return includeDay ? $"{s.DayName(local.DayOfWeek)} {hhmm}" : hhmm;
    }

    /// <summary>Rounded percent for display, or null when unknown.</summary>
    public static int? Percent(UsageWindow? window) =>
        window?.Utilization is { } u ? (int)Math.Round(u, MidpointRounding.AwayFromZero) : null;

    /// <summary>Green below 50, orange 50 to 80, red above 80.</summary>
    public static UsageLevel Level(double percent) => percent switch
    {
        < 50 => UsageLevel.Low,
        <= 80 => UsageLevel.Medium,
        _ => UsageLevel.High,
    };

    /// <summary>Tooltip text, e.g. "5h: 21% | Week: 74%".</summary>
    public static string Tooltip(UsageData? data)
    {
        static string P(UsageWindow? w) => Percent(w) is { } p ? $"{p}%" : "—";
        return $"5h: {P(data?.FiveHour)} | Week: {P(data?.SevenDay)}";
    }
}
