using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GreenHerb.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetCurrentUserId(this ClaimsPrincipal principal)
    {
        var rawValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("nameid")?.Value;

        return int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            ? userId
            : null;
    }
}
