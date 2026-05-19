using GreenHerb.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GreenHerb.Api.Services;

public sealed class SessionHintService : ISessionHintService
{
    private const string SessionHintCookieName = "__session_hint";
    private const int SessionHintVersion = 1;
    private const int DefaultSessionHintTtlSeconds = 7 * 24 * 60 * 60;
    private const int MinSessionHintTtlSeconds = 60;
    private const int MaxSessionHintTtlSeconds = 7 * 24 * 60 * 60;

    private readonly AuthCookieOptions _authCookieOptions;
    private readonly SessionHintOptions _sessionHintOptions;

    public SessionHintService(
        IOptions<AuthCookieOptions> authCookieOptions,
        IOptions<SessionHintOptions> sessionHintOptions)
    {
        _authCookieOptions = authCookieOptions.Value;
        _sessionHintOptions = sessionHintOptions.Value;
    }

    public void SetSessionHintCookie(HttpContext httpContext, int sessionId)
    {
        var ttlSeconds = GetSessionHintTtlSeconds();
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);
        var signedHint = GenerateSessionHintToken(sessionId, expiresAt);

        httpContext.Response.Cookies.Append(SessionHintCookieName, signedHint, new CookieOptions
        {
            HttpOnly = true,
            Secure = _authCookieOptions.UseSecureAuthCookies,
            SameSite = _authCookieOptions.UseCrossSiteAuth ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(ttlSeconds),
            Expires = expiresAt
        });
    }

    public void ClearSessionHintCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Append(SessionHintCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = _authCookieOptions.UseSecureAuthCookies,
            SameSite = _authCookieOptions.UseCrossSiteAuth ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.Zero,
            Expires = DateTimeOffset.UnixEpoch
        });
    }

    private int GetSessionHintTtlSeconds()
    {
        if (_sessionHintOptions.TtlSeconds <= 0)
        {
            return DefaultSessionHintTtlSeconds;
        }

        return Math.Clamp(_sessionHintOptions.TtlSeconds, MinSessionHintTtlSeconds, MaxSessionHintTtlSeconds);
    }

    private string GenerateSessionHintToken(int sessionId, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(_sessionHintOptions.Key))
        {
            throw new InvalidOperationException("SessionHint:Key is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_sessionHintOptions.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sid", sessionId.ToString(CultureInfo.InvariantCulture)),
            new("ver", SessionHintVersion.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _sessionHintOptions.Issuer,
            audience: _sessionHintOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
