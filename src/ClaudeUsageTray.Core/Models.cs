namespace ClaudeUsageTray.Core;

/// <summary>One usage window (5-hour or 7-day). Any field can be missing in the API response.</summary>
public sealed record UsageWindow(double? Utilization, DateTimeOffset? ResetsAt);

/// <summary>Usage data parsed from the API.</summary>
public sealed record UsageData(UsageWindow? FiveHour, UsageWindow? SevenDay);

/// <summary>Usage data plus the time it was fetched.</summary>
public sealed record UsageSnapshot(UsageData Data, DateTimeOffset FetchedAt);

/// <summary>Token read from the Claude Code credentials file.</summary>
public sealed record OAuthCredentials(string AccessToken, DateTimeOffset? ExpiresAt);

public enum FetchStatus
{
    Success,
    NoCredentials,
    TokenExpired,
    RateLimited,
    NetworkError,
    HttpError,
    UnexpectedFormat,
}

/// <summary>Result of one fetch attempt. <see cref="Data"/> is set only on success.</summary>
public sealed record FetchResult(FetchStatus Status, UsageData? Data = null, int? HttpStatusCode = null)
{
    public bool IsSuccess => Status == FetchStatus.Success && Data is not null;

    public static FetchResult Ok(UsageData data) => new(FetchStatus.Success, data);
    public static FetchResult Fail(FetchStatus status, int? httpStatusCode = null) => new(status, null, httpStatusCode);
}

public enum UsageLevel
{
    Low,
    Medium,
    High,
}

public enum Language
{
    English,
    Thai,
}
