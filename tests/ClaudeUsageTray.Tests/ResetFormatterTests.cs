using ClaudeUsageTray.Core;

namespace ClaudeUsageTray.Tests;

public class ResetFormatterTests
{
    // Fixed UTC+7 zone so tests do not depend on the machine's time zone.
    private static readonly TimeZoneInfo Bangkok =
        TimeZoneInfo.CreateCustomTimeZone("Test+7", TimeSpan.FromHours(7), "Test+7", "Test+7");

    // Wednesday 2026-11-25 12:15 local (05:15 UTC).
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 5, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Format_HoursAndMinutes_Thai()
    {
        var text = ResetFormatter.Format(Now.AddHours(2).AddMinutes(15), Now, Bangkok, Strings.Thai, includeDay: false);

        Assert.Equal("รีเซ็ตใน 2 ชม. 15 นาที", text.Countdown);
        Assert.Equal("14:30", text.At);
        Assert.Equal("รีเซ็ตใน 2 ชม. 15 นาที (14:30)", text.Combined);
    }

    [Fact]
    public void Format_HoursAndMinutes_English()
    {
        var text = ResetFormatter.Format(Now.AddHours(2).AddMinutes(15), Now, Bangkok, Strings.English, includeDay: false);

        Assert.Equal("Resets in 2 h 15 min (14:30)", text.Combined);
    }

    [Fact]
    public void Format_WeeklyWithDay_ShowsDayName()
    {
        // Friday 2026-11-27 09:00 local.
        var reset = new DateTimeOffset(2026, 11, 27, 2, 0, 0, TimeSpan.Zero);

        var th = ResetFormatter.Format(reset, Now, Bangkok, Strings.Thai, includeDay: true);
        var en = ResetFormatter.Format(reset, Now, Bangkok, Strings.English, includeDay: true);

        Assert.Equal("รีเซ็ตใน 1 วัน 20 ชม. (ศ. 09:00)", th.Combined);
        Assert.Equal("Resets in 1 d 20 h (Fri 09:00)", en.Combined);
    }

    [Fact]
    public void Format_MinutesOnly()
    {
        var text = ResetFormatter.Format(Now.AddMinutes(34).AddSeconds(50), Now, Bangkok, Strings.English, false);

        Assert.Equal("Resets in 34 min", text.Countdown);
    }

    [Fact]
    public void Format_LessThanOneMinute()
    {
        var text = ResetFormatter.Format(Now.AddSeconds(20), Now, Bangkok, Strings.Thai, false);

        Assert.Equal("รีเซ็ตใน < 1 นาที", text.Countdown);
    }

    [Fact]
    public void Format_ResetsAtIsNull_ShowsNoData()
    {
        var text = ResetFormatter.Format(null, Now, Bangkok, Strings.Thai, false);

        Assert.Equal("ไม่มีข้อมูล", text.Countdown);
        Assert.Null(text.At);
        Assert.Equal("ไม่มีข้อมูล", text.Combined);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3600)]
    public void Format_ResetsAtInPast_ShowsResetDone(int secondsFromNow)
    {
        var text = ResetFormatter.Format(Now.AddSeconds(secondsFromNow), Now, Bangkok, Strings.English, true);

        Assert.Equal("Reset done, waiting for new data", text.Countdown);
        Assert.Null(text.At);
    }

    [Fact]
    public void FormatTime_ConvertsUtcToLocalZone()
    {
        var utc = new DateTimeOffset(2026, 11, 25, 23, 0, 0, TimeSpan.Zero); // Thu 06:00 at UTC+7

        Assert.Equal("พฤ. 06:00", ResetFormatter.FormatTime(utc, Bangkok, Strings.Thai, includeDay: true));
    }

    [Theory]
    [InlineData(0, UsageLevel.Low)]
    [InlineData(49.9, UsageLevel.Low)]
    [InlineData(50, UsageLevel.Medium)]
    [InlineData(80, UsageLevel.Medium)]
    [InlineData(80.1, UsageLevel.High)]
    [InlineData(120, UsageLevel.High)]
    public void Level_UsesThresholds(double percent, UsageLevel expected)
    {
        Assert.Equal(expected, ResetFormatter.Level(percent));
    }

    [Fact]
    public void Percent_RoundsAndHandlesNull()
    {
        Assert.Equal(22, ResetFormatter.Percent(new UsageWindow(21.5, null)));
        Assert.Null(ResetFormatter.Percent(new UsageWindow(null, null)));
        Assert.Null(ResetFormatter.Percent(null));
    }

    [Fact]
    public void Tooltip_FormatsBothValues()
    {
        var data = new UsageData(new UsageWindow(21, null), new UsageWindow(74.4, null));

        Assert.Equal("5h: 21% | Week: 74%", ResetFormatter.Tooltip(data));
        Assert.Equal("5h: — | Week: —", ResetFormatter.Tooltip(null));
    }
}
