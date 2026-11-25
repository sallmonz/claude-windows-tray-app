using System.Net;
using ClaudeUsageTray.Core;

namespace ClaudeUsageTray.Tests;

public sealed class UsageClientTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 5, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Directory.CreateTempSubdirectory("usage-tests-").FullName;
    private string CredsPath => Path.Combine(_dir, ".credentials.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteCreds(long? expiresAtMs = null)
    {
        var exp = expiresAtMs is { } ms ? $", \"expiresAt\": {ms}" : "";
        File.WriteAllText(CredsPath, $$"""{ "claudeAiOauth": { "accessToken": "tok"{{exp}} } }""");
    }

    private (UsageClient Client, FakeHandler Handler) Create(Func<HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        return (new UsageClient(new HttpClient(handler), CredsPath, () => Now, null), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task Success_SendsHeadersAndParses()
    {
        WriteCreds(Now.AddHours(1).ToUnixTimeMilliseconds());
        var (client, handler) = Create(() => Json(HttpStatusCode.OK,
            """{ "five_hour": { "utilization": 21, "resets_at": null }, "seven_day": null }"""));

        var result = await client.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(21, result.Data!.FiveHour!.Utilization);
        Assert.Equal("Bearer tok", handler.LastRequest!.Headers.Authorization!.ToString());
        Assert.Equal("oauth-2025-04-20", handler.LastRequest.Headers.GetValues("anthropic-beta").Single());
        Assert.Equal(UsageClient.UsageUrl, handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task NoCredentialsFile_ReturnsNoCredentials()
    {
        var (client, handler) = Create(() => throw new InvalidOperationException("must not call"));

        var result = await client.FetchAsync();

        Assert.Equal(FetchStatus.NoCredentials, result.Status);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsTokenExpiredWithoutCallingApi()
    {
        WriteCreds(Now.AddMinutes(-1).ToUnixTimeMilliseconds());
        var (client, handler) = Create(() => throw new InvalidOperationException("must not call"));

        var result = await client.FetchAsync();

        Assert.Equal(FetchStatus.TokenExpired, result.Status);
        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, FetchStatus.TokenExpired)]
    [InlineData(HttpStatusCode.TooManyRequests, FetchStatus.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, FetchStatus.HttpError)]
    [InlineData(HttpStatusCode.InternalServerError, FetchStatus.HttpError)]
    public async Task ErrorStatus_IsMapped(HttpStatusCode code, FetchStatus expected)
    {
        WriteCreds();
        var (client, _) = Create(() => Json(code, "{}"));

        var result = await client.FetchAsync();

        Assert.Equal(expected, result.Status);
        Assert.Equal((int)code, result.HttpStatusCode);
    }

    [Fact]
    public async Task ChangedFormat_ReturnsUnexpectedFormat()
    {
        WriteCreds();
        var (client, _) = Create(() => Json(HttpStatusCode.OK, """{ "limits": [] }"""));

        Assert.Equal(FetchStatus.UnexpectedFormat, (await client.FetchAsync()).Status);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsNetworkError()
    {
        WriteCreds();
        var (client, _) = Create(() => throw new HttpRequestException("offline"));

        Assert.Equal(FetchStatus.NetworkError, (await client.FetchAsync()).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CredentialsPath_DefaultsToUserProfile(string? configDir)
    {
        Assert.Equal(Path.Combine("home", ".claude", ".credentials.json"),
            UsageClient.CredentialsPathFor(configDir, "home"));
    }

    [Fact]
    public void CredentialsPath_UsesClaudeConfigDir()
    {
        Assert.Equal(Path.Combine("custom", ".credentials.json"), UsageClient.CredentialsPathFor("custom", "home"));
    }

    private sealed class FakeHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(respond());
        }
    }
}
