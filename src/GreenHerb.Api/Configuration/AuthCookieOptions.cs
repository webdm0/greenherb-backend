namespace GreenHerb.Api.Configuration;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Cookies";

    public bool UseCrossSiteAuth { get; init; }
    public bool UseSecureAuthCookies { get; init; }
}
