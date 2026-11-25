using ClaudeUsageTray.Core;

namespace ClaudeUsageTray.Tests;

public class UsageStateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 25, 5, 0, 0, TimeSpan.Zero);
    private static readonly UsageData Data = new(new UsageWindow(21, null), null);

    [Fact]
    public void Success_StoresSnapshot()
    {
        var state = new UsageState();
        state.Apply(FetchResult.Ok(Data), T0);

        Assert.Same(Data, state.Snapshot!.Data);
        Assert.Equal(T0, state.Snapshot.FetchedAt);
        Assert.False(state.IsStale);
    }

    [Fact]
    public void FailureAfterSuccess_KeepsOldDataAndMarksStale()
    {
        var state = new UsageState();
        state.Apply(FetchResult.Ok(Data), T0);
        state.Apply(FetchResult.Fail(FetchStatus.NetworkError), T0.AddMinutes(3));

        Assert.Same(Data, state.Snapshot!.Data);
        Assert.True(state.IsStale);
        Assert.Equal(FetchStatus.NetworkError, state.LastError!.Status);
    }

    [Fact]
    public void FailureWithoutData_IsNotStale()
    {
        var state = new UsageState();
        state.Apply(FetchResult.Fail(FetchStatus.NoCredentials), T0);

        Assert.Null(state.Snapshot);
        Assert.False(state.IsStale);
        Assert.NotNull(state.LastError);
    }

    [Fact]
    public void SuccessAfterFailure_ClearsError()
    {
        var state = new UsageState();
        state.Apply(FetchResult.Fail(FetchStatus.RateLimited, 429), T0);
        state.Apply(FetchResult.Ok(Data), T0.AddMinutes(3));

        Assert.Null(state.LastError);
        Assert.False(state.IsStale);
    }
}
