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
    public static OAuthCredentials? ParseCredentials(string json) => ParseCredentials(json, out _);

    /// <summary>
    /// Same as <see cref="ParseCredentials(string)"/>, and on failure explains why in <paramref name="problem"/>.
    /// The explanation only has key names and value types, never secret values, so it is safe to log.
    /// Key names are matched case-insensitively, and comments and trailing commas are allowed.
    /// </summary>
    public static OAuthCredentials? ParseCredentials(string json, out string? problem)
    {
        problem = null;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json.TrimStart('\uFEFF'), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException ex)
        {
            problem = $"not valid JSON (line {ex.LineNumber + 1}, byte {ex.BytePositionInLine}), length {json.Length}";
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                problem = $"root is {root.ValueKind}, not an object";
                return null;
            }

            if (FindProperty(root, "claudeAiOauth") is not { } oauth)
            {
                problem = $"no 'claudeAiOauth' key. Top-level keys: {DescribeKeys(root)}";
                return null;
            }
            if (oauth.ValueKind != JsonValueKind.Object)
            {
                problem = $"'claudeAiOauth' is {oauth.ValueKind}, not an object";
                return null;
            }

            if (FindProperty(oauth, "accessToken") is not { } tokenEl)
            {
                problem = $"no 'accessToken' key. claudeAiOauth keys: {DescribeKeys(oauth)}";
                return null;
            }
            if (tokenEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(tokenEl.GetString()))
            {
                problem = $"'accessToken' is {tokenEl.ValueKind} or empty";
                return null;
            }

            DateTimeOffset? expiresAt = null;
            if (FindProperty(oauth, "expiresAt") is { } exp)
            {
                if (exp.ValueKind == JsonValueKind.Number && exp.TryGetDouble(out var ms))
                    expiresAt = DateTimeOffset.FromUnixTimeMilliseconds((long)ms);
                else if (exp.ValueKind == JsonValueKind.String &&
                         long.TryParse(exp.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var msText))
                    expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(msText);
            }

            return new OAuthCredentials(tokenEl.GetString()!, expiresAt);
        }
    }

    /// <summary>Exact match first, then case-insensitive.</summary>
    private static JsonElement? FindProperty(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var exact))
            return exact;
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    private static string DescribeKeys(JsonElement obj)
    {
        var keys = obj.EnumerateObject().Select(p => $"{p.Name}({p.Value.ValueKind})").ToList();
        return keys.Count == 0 ? "(none)" : string.Join(", ", keys);
    }
}
