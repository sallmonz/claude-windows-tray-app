using ClaudeUsageTray.Core;

namespace ClaudeUsageTray.Tests;

public class UsageParserTests
{
    [Fact]
    public void ParseUsage_NormalResponse_ReadsBothWindows()
    {
        const string json = """
            {
              "five_hour": { "utilization": 21.0, "resets_at": "2026-11-25T07:30:00+00:00" },
              "seven_day": { "utilization": 74.0, "resets_at": "2026-11-27T02:00:00.123456+00:00" },
              "seven_day_opus": null,
              "extra_field": { "anything": 1 }
            }
            """;

        var data = UsageParser.ParseUsage(json);

        Assert.Equal(21.0, data.FiveHour!.Utilization);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 7, 30, 0, TimeSpan.Zero), data.FiveHour.ResetsAt);
        Assert.Equal(74.0, data.SevenDay!.Utilization);
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 2, 0, 0, TimeSpan.Zero),
            data.SevenDay.ResetsAt!.Value.AddTicks(-1234560));
    }

    [Fact]
    public void ParseUsage_WindowIsNull_ReturnsNullWindow()
    {
        var data = UsageParser.ParseUsage("""{ "five_hour": null, "seven_day": { "utilization": 5, "resets_at": null } }""");

        Assert.Null(data.FiveHour);
        Assert.Equal(5.0, data.SevenDay!.Utilization);
        Assert.Null(data.SevenDay.ResetsAt);
    }

    [Fact]
    public void ParseUsage_FieldsInsideWindowAreNullOrMissing_ReturnsNulls()
    {
        var data = UsageParser.ParseUsage("""{ "five_hour": { "utilization": null }, "seven_day": {} }""");

        Assert.Null(data.FiveHour!.Utilization);
        Assert.Null(data.FiveHour.ResetsAt);
        Assert.Null(data.SevenDay!.Utilization);
    }

    [Fact]
    public void ParseUsage_OnlyOneWindowPresent_OtherIsNull()
    {
        var data = UsageParser.ParseUsage("""{ "seven_day": { "utilization": 1 } }""");

        Assert.Null(data.FiveHour);
        Assert.NotNull(data.SevenDay);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "other": 1 }""")]
    [InlineData("""{ "five_hour": "21%" }""")]
    [InlineData("""{ "five_hour": { "utilization": "21" } }""")]
    [InlineData("""{ "five_hour": { "resets_at": "tomorrow" } }""")]
    public void ParseUsage_UnexpectedShape_ThrowsFormatException(string json)
    {
        Assert.Throws<FormatException>(() => UsageParser.ParseUsage(json));
    }

    [Fact]
    public void ParseCredentials_ReadsTokenAndExpiry()
    {
        const string json = """
            { "claudeAiOauth": { "accessToken": "sk-ant-oat01-abc", "refreshToken": "r",
              "expiresAt": 1764000000000, "scopes": ["user:inference"], "subscriptionType": "max" } }
            """;

        var creds = UsageParser.ParseCredentials(json);

        Assert.Equal("sk-ant-oat01-abc", creds!.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1764000000000), creds.ExpiresAt);
    }

    [Fact]
    public void ParseCredentials_NoExpiry_ExpiresAtIsNull()
    {
        var creds = UsageParser.ParseCredentials("""{ "claudeAiOauth": { "accessToken": "t" } }""");

        Assert.Equal("t", creds!.AccessToken);
        Assert.Null(creds.ExpiresAt);
    }

    [Fact]
    public void ParseCredentials_IsLenientLikePowerShell()
    {
        // BOM, different key case, comment, trailing comma, expiresAt as float.
        const string json = "\uFEFF{ // written by a tool\n \"ClaudeAiOauth\": { \"AccessToken\": \"t\", \"expiresAt\": 1764000000000.0, }, }";

        var creds = UsageParser.ParseCredentials(json);

        Assert.Equal("t", creds!.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1764000000000), creds.ExpiresAt);
    }

    [Fact]
    public void ParseCredentials_Failure_ExplainsWithoutSecrets()
    {
        const string json = """{ "claudeAiOauth": { "token": "secret-value" }, "mcpOAuth": {} }""";

        Assert.Null(UsageParser.ParseCredentials(json, out var problem));
        Assert.Contains("claudeAiOauth keys: token(String)", problem);
        Assert.DoesNotContain("secret-value", problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("""{ "mcpOAuth": {} }""")]
    [InlineData("""{ "claudeAiOauth": null }""")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "" } }""")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": null } }""")]
    public void ParseCredentials_NoUsableToken_ReturnsNull(string json)
    {
        Assert.Null(UsageParser.ParseCredentials(json));
    }
}
