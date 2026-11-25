using System.Globalization;
using System.Text.Json;

namespace ClaudeUsageTray.Core;

/// <summary>
/// Pure JSON parsing for the usage API and the credentials file.
/// The usage endpoint is not an official API, so all format knowledge lives here and in UsageClient.
/// </summary>
public static class UsageParser
{
    /// <summary>
    /// Parses the body of GET /api/oauth/usage.
    /// Throws <see cref="FormatException"/> when the shape no longer matches what we expect.
    /// </summary>
    public static UsageData ParseUsage(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Usage response is not valid JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("Usage response is not a JSON object.");

            bool hasFive = root.TryGetProperty("five_hour", out var five);
            bool hasSeven = root.TryGetProperty("seven_day", out var seven);
            if (!hasFive && !hasSeven)
                throw new FormatException("Usage response has neither 'five_hour' nor 'seven_day'.");

            return new UsageData(
                hasFive ? ParseWindow(five, "five_hour") : null,
                hasSeven ? ParseWindow(seven, "seven_day") : null);
        }
    }

    private static UsageWindow? ParseWindow(JsonElement e, string name)
    {
        if (e.ValueKind == JsonValueKind.Null)
            return null;
        if (e.ValueKind != JsonValueKind.Object)
            throw new FormatException($"'{name}' is not an object.");

        double? utilization = null;
        if (e.TryGetProperty("utilization", out var u) && u.ValueKind != JsonValueKind.Null)
        {
            if (u.ValueKind != JsonValueKind.Number)
                throw new FormatException($"'{name}.utilization' is not a number.");
            utilization = u.GetDouble();
        }

        DateTimeOffset? resetsAt = null;
        if (e.TryGetProperty("resets_at", out var r) && r.ValueKind != JsonValueKind.Null)
        {
            if (r.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed))
                throw new FormatException($"'{name}.resets_at' is not an ISO 8601 date.");
            resetsAt = parsed;
        }

        return new UsageWindow(utilization, resetsAt);
    }

    /// <summary>
    /// Reads claudeAiOauth.accessToken and claudeAiOauth.expiresAt (Unix ms) from .credentials.json.
    /// Returns null when the file has no usable token.
    /// </summary>
    public static OAuthCredentials? ParseCredentials(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object ||
                !oauth.TryGetProperty("accessToken", out var tokenEl) ||
                tokenEl.ValueKind != JsonValueKind.String)
                return null;

            var token = tokenEl.GetString();
            if (string.IsNullOrWhiteSpace(token))
                return null;

            DateTimeOffset? expiresAt = null;
            if (oauth.TryGetProperty("expiresAt", out var exp) &&
                exp.ValueKind == JsonValueKind.Number &&
                exp.TryGetInt64(out var ms))
                expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);

            return new OAuthCredentials(token, expiresAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
