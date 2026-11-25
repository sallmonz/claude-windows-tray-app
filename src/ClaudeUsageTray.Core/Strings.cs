namespace ClaudeUsageTray.Core;

/// <summary>All user-facing text in English and Thai.</summary>
public sealed class Strings
{
    public static readonly Strings English = new(Language.English);
    public static readonly Strings Thai = new(Language.Thai);

    public static Strings For(Language language) => language == Language.Thai ? Thai : English;

    private Strings(Language language) => Language = language;

    public Language Language { get; }
    private bool Th => Language == Language.Thai;

    public string FiveHourLabel => Th ? "รอบ 5 ชั่วโมง" : "5-hour window";
    public string WeeklyLabel => Th ? "รายสัปดาห์" : "Weekly";
    public string LastUpdated => Th ? "อัปเดตล่าสุด" : "Last updated";
    public string Loading => Th ? "กำลังโหลด…" : "Loading…";
    public string NoData => Th ? "ไม่มีข้อมูล" : "No data";
    public string ResetPassed => Th ? "รีเซ็ตแล้ว รอข้อมูลใหม่" : "Reset done, waiting for new data";

    public string MenuRefresh => Th ? "รีเฟรชตอนนี้" : "Refresh now";
    public string MenuStartWithWindows => Th ? "เริ่มพร้อม Windows" : "Start with Windows";
    public string MenuExit => Th ? "ออก" : "Exit";
    public string MenuIconFiveHour => Th ? "ไอคอนแสดงรอบ 5 ชม." : "Icon shows 5-hour";
    public string MenuIconWeekly => Th ? "ไอคอนแสดงรายสัปดาห์" : "Icon shows weekly";
    public string MenuIconPercent => Th ? "แสดง % บนไอคอน" : "Show % on icon";

    public string ResetsIn(string duration) => Th ? $"รีเซ็ตใน {duration}" : $"Resets in {duration}";
    public string At(string time) => Th ? $"เวลา {time}" : $"at {time}";

    public string Days(int d, int h) => Th ? $"{d} วัน {h} ชม." : $"{d} d {h} h";
    public string Hours(int h, int m) => Th ? $"{h} ชม. {m} นาที" : $"{h} h {m} min";
    public string Minutes(int m) => Th ? $"{m} นาที" : $"{m} min";
    public string LessThanMinute => Th ? "< 1 นาที" : "< 1 min";

    private static readonly string[] ThaiDays = ["อา.", "จ.", "อ.", "พ.", "พฤ.", "ศ.", "ส."];
    private static readonly string[] EnglishDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    public string DayName(DayOfWeek day) => (Th ? ThaiDays : EnglishDays)[(int)day];

    public string Stale(string reason, string time) =>
        Th ? $"ข้อมูลเก่า — {reason} ({time})" : $"Data is stale — {reason} ({time})";

    public string Error(FetchStatus status, int? httpStatusCode) => status switch
    {
        FetchStatus.NoCredentials => Th
            ? "ไม่พบการล็อกอิน Claude Code CLI\nเปิด terminal พิมพ์ claude แล้ว /login"
            : "Claude Code CLI login not found.\nOpen a terminal, run claude, then /login",
        FetchStatus.TokenExpired => Th
            ? "Token หมดอายุ\nเปิด terminal พิมพ์ claude เพื่อ refresh"
            : "Token expired.\nRun claude in a terminal to refresh it",
        FetchStatus.RateLimited => Th ? "ถูกจำกัดการเรียก (429)" : "Rate limited (429)",
        FetchStatus.NetworkError => Th ? "เชื่อมต่อไม่ได้" : "Connection failed",
        FetchStatus.HttpError => Th
            ? $"API ตอบกลับ {httpStatusCode} อาจต้องอัปเดตแอป"
            : $"API returned {httpStatusCode}. The app may need an update",
        FetchStatus.UnexpectedFormat => Th
            ? "รูปแบบข้อมูล API เปลี่ยน ต้องอัปเดตแอป"
            : "API format changed. The app needs an update",
        _ => "",
    };
}
