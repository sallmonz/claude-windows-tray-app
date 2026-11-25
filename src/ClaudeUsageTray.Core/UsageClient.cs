using System.Net;
using System.Net.Http.Headers;

namespace ClaudeUsageTray.Core;

/// <summary>
/// Calls the unofficial Claude usage endpoint. If the API changes, this class and UsageParser
/// are the only places to update.
/// </summary>
public sealed class UsageClient
{
    public const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    public const string BetaHeader = "oauth-2025-04-20";

    private static readonly HttpClient SharedHttp = CreateHttpClient();

    private readonly HttpClient _http;
    private readonly string _credentialsPath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<string> _log;

    public UsageClient(Action<string>? log = null)
        : this(SharedHttp, DefaultCredentialsPath, () => DateTimeOffset.UtcNow, log)
    {
    }

    internal UsageClient(HttpClient http, string credentialsPath, Func<DateTimeOffset> clock, Action<string>? log)
    {
        _http = http;
        _credentialsPath = credentialsPath;
        _clock = clock;
        _log = log ?? (_ => { });
    }

    /// <summary>
    /// %CLAUDE_CONFIG_DIR%\.credentials.json when Claude Code is configured to use another folder,
    /// otherwise %USERPROFILE%\.claude\.credentials.json.
    /// </summary>
    public static string DefaultCredentialsPath => CredentialsPathFor(
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static string CredentialsPathFor(string? configDir, string userProfile) =>
        Path.Combine(string.IsNullOrWhiteSpace(configDir) ? Path.Combine(userProfile, ".claude") : configDir,
            ".credentials.json");

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClaudeUsageTray", "1.0"));
        return http;
    }

    public async Task<FetchResult> FetchAsync(CancellationToken ct = default)
    {
        // Read the file every time: Claude Code refreshes the token now and then.
        var creds = await ReadCredentialsAsync(ct).ConfigureAwait(false);
        if (creds is null)
            return FetchResult.Fail(FetchStatus.NoCredentials);

        if (creds.ExpiresAt is { } exp && exp <= _clock())
        {
            _log($"Token expired at {exp:O}.");
            return FetchResult.Fail(FetchStatus.TokenExpired);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.Add("anthropic-beta", BetaHeader);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _log($"Network error: {ex.GetType().Name}: {ex.Message}");
            return FetchResult.Fail(FetchStatus.NetworkError);
        }

        using (response)
        {
            int code = (int)response.StatusCode;
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.Unauthorized:
                    _log("API returned 401 Unauthorized.");
                    return FetchResult.Fail(FetchStatus.TokenExpired, code);
                case HttpStatusCode.TooManyRequests:
                    _log("API returned 429 Too Many Requests.");
                    return FetchResult.Fail(FetchStatus.RateLimited, code);
                default:
                    _log($"API returned {code}. Body: {Truncate(body)}");
                    return FetchResult.Fail(FetchStatus.HttpError, code);
            }

            try
            {
                return FetchResult.Ok(UsageParser.ParseUsage(body));
            }
            catch (FormatException ex)
            {
                _log($"Unexpected usage format: {ex.Message} Body: {Truncate(body)}");
                return FetchResult.Fail(FetchStatus.UnexpectedFormat, code);
            }
        }
    }

    private async Task<OAuthCredentials?> ReadCredentialsAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                _log($"Credentials file not found: {_credentialsPath}");
                return null;
            }

            var json = await File.ReadAllTextAsync(_credentialsPath, ct).ConfigureAwait(false);
            var creds = UsageParser.ParseCredentials(json, out var problem);
            if (creds is null)
                _log($"Credentials file has no usable claudeAiOauth.accessToken: {problem}");
            return creds;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log($"Cannot read credentials file: {ex.Message}");
            return null;
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
