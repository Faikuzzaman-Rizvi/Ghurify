using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// Issues short-lived JWT access tokens and opaque refresh tokens.
///
/// The access token carries only the user id. The phone number is deliberately left out:
/// a JWT is not encrypted, so anything in it is readable by whoever holds it, and personal
/// data has no business travelling in a bearer token.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<IdentityOptions> options, IClock clock) : ITokenIssuer
{
    /// <summary>256 bits of entropy: far beyond guessing.</summary>
    private const int RefreshTokenBytes = 32;

    private readonly IdentityOptions _options = options.Value;
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.JwtSigningKey)),
        SecurityAlgorithms.HmacSha256);

    public AccessToken IssueAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = clock.UtcNow;
        var expiresOn = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            // A unique id per token, so a single token can be revoked or traced later.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.JwtIssuer,
            audience: _options.JwtAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresOn.UtcDateTime,
            signingCredentials: _credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(value, expiresOn, _options.AccessTokenMinutes * 60);
    }

    public (string Token, byte[] Hash) IssueRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenBytes);

        // URL-safe: the value travels in a cookie.
        var token = Base64UrlEncoder.Encode(bytes);

        return (token, HashRefreshToken(token));
    }

    /// <summary>
    /// Plain SHA-256, not a password hash. The token is 256 random bits, so there is no
    /// low-entropy secret to protect with a slow KDF, and refresh happens often enough that
    /// the cost would be felt.
    /// </summary>
    public byte[] HashRefreshToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
