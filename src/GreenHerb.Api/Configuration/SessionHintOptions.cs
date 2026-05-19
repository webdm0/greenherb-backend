namespace GreenHerb.Api.Configuration;

public sealed class SessionHintOptions
{
    public const string SectionName = "SessionHint";

    public string Key { get; init; } = string.Empty;
    public int TtlSeconds { get; init; } = 7 * 24 * 60 * 60;
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
}
