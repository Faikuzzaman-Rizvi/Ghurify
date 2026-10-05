using System.Security.Cryptography;
using System.Text;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// Generates and checks one-time codes.
///
/// A six-digit code has only a million possibilities, so a plain hash of it could be reversed
/// from a leaked table almost instantly. Two things prevent that: the hash is keyed with a
/// server-side pepper that never goes in the database, and it is bound to the address so
/// one account hash cannot be replayed against another.
/// </summary>
public sealed class OtpCodeService(IOptions<IdentityOptions> options) : IOtpCodeService
{
    private readonly IdentityOptions _options = options.Value;
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(options.Value.OtpPepper);

    public string GenerateCode()
    {
        // RandomNumberGenerator, not Random: a predictable code is a bypass of the whole flow.
        var max = (int)Math.Pow(10, _options.OtpCodeLength);
        var value = RandomNumberGenerator.GetInt32(0, max);

        return value.ToString(
            System.Globalization.CultureInfo.InvariantCulture)
            .PadLeft(_options.OtpCodeLength, '0');
    }

    public byte[] Hash(EmailAddress email, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{email.Value}:{code}");
        return HMACSHA256.HashData(_pepper, payload);
    }

    public bool Matches(EmailAddress email, string code, byte[] expectedHash)
    {
        if (string.IsNullOrEmpty(code) || expectedHash is null)
        {
            return false;
        }

        var actual = Hash(email, code);

        // Fixed-time compare: a byte-by-byte comparison leaks how much of the code was right.
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }
}
