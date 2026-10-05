using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ghurify.Api.Endpoints;

/// <summary>Reads the signed-in user's id from the access token's claims.</summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The account id from the token's subject, or null for an anonymous caller or a token
    /// whose subject is not an id. The JWT handler maps "sub" to NameIdentifier by default, so
    /// both are checked.
    /// </summary>
    public static long? FindUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return long.TryParse(subject, CultureInfo.InvariantCulture, out var userId) ? userId : null;
    }
}
