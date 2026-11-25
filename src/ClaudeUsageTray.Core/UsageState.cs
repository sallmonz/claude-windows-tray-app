namespace ClaudeUsageTray.Core;

/// <summary>
/// Keeps the last good snapshot and the last error. On failure the old data stays visible but is marked stale.
/// </summary>
public sealed class UsageState
{
    public UsageSnapshot? Snapshot { get; private set; }
    public FetchResult? LastError { get; private set; }
    public DateTimeOffset? LastErrorAt { get; private set; }

    /// <summary>True when there is old data but the latest fetch failed.</summary>
    public bool IsStale => Snapshot is not null && LastError is not null;

    public void Apply(FetchResult result, DateTimeOffset now)
    {
        if (result.IsSuccess)
        {
            Snapshot = new UsageSnapshot(result.Data!, now);
            LastError = null;
            LastErrorAt = null;
        }
        else
        {
            LastError = result;
            LastErrorAt = now;
        }
    }
}
