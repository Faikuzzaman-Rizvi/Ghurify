using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ghurify.Application.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// Step-up receipts as short-lived signed tokens.
///
/// Signed with the same key as access tokens but issued to a different audience, which is what
/// keeps the two apart: the JWT bearer handler only accepts <c>JwtAudience</c>, so a receipt can
/// never be used to authenticate a request, and this validator only accepts
/// <see cref="StepUpAudience"/>, so an access token can never be passed off as a receipt.
///
/// Nothing is stored. A receipt therefore cannot be revoked before it expires, which is why the
/// lifetime is minutes: it is proof of a password typed just now, not a session.
/// </summary>
public sealed class StepUpTokens(IOptions<IdentityOptions> options, IClock clock) : IStepUpTokens
{
    /// <summary>Audience that marks a token as step-up proof and nothing else.</summary>
    public const string StepUpAudience = "ghurify-step-up";

    private readonly IdentityOptions _options = options.Value;
    private readonly SymmetricSecurityKey _key =
        new(Encoding.UTF8.GetBytes(options.Value.JwtSigningKey));

    private static readonly JwtSecurityTokenHandler Handler = new();

    public StepUpToken Issue(long userId)
    {
        var now = clock.UtcNow;
        var expiresOn = now.AddMinutes(_options.StepUpMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.JwtIssuer,
            audience: StepUpAudience,
            claims:
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    userId.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: expiresOn.UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new StepUpToken(
            Handler.WriteToken(token),
            expiresOn,
            _options.StepUpMinutes * 60);
    }

    public bool IsValidFor(string? token, long userId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var principal = Handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = StepUpAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _key,
                    ValidateLifetime = true,
                    // A receipt is minutes long; the default five-minute grace would be most of it.
                    ClockSkew = TimeSpan.FromSeconds(30),
                },
                out _);

            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Bound to the account: a genuine receipt for somebody else proves nothing here.
            return long.TryParse(subject, CultureInfo.InvariantCulture, out var tokenUserId)
                && tokenUserId == userId;
        }
        catch (SecurityTokenException)
        {
            // Expired, tampered with, or meant for something else. All the same answer: no.
            return false;
        }
        catch (ArgumentException)
        {
            // Not a JWT at all.
            return false;
        }
    }
}
