using System.Security.Cryptography;
using System.Text;
using Ghurify.Application.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256 from the .NET base library, so no extra package: a random 16-byte salt
/// per password, a 32-byte hash, and the work factor stored with each hash so it can be raised
/// later without breaking anyone's sign-in.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IOptions<IdentityOptions> options) : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly int _iterations = options.Value.PasswordIterations;

    /// <summary>What <see cref="VerifyAgainstNothing"/> compares with: fixed, and never anyone's.</summary>
    private readonly PasswordHash _nothing = new(new byte[HashBytes], new byte[SaltBytes], options.Value.PasswordIterations);

    public PasswordHash Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        return new PasswordHash(Derive(password, salt, _iterations), salt, _iterations);
    }

    public bool Verify(string password, PasswordHash stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var actual = Derive(password, stored.Salt, stored.Iterations);
        return CryptographicOperations.FixedTimeEquals(actual, stored.Hash);
    }

    public void VerifyAgainstNothing(string password) => Verify(string.IsNullOrEmpty(password) ? "-" : password, _nothing);

    public bool NeedsRehash(PasswordHash stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return stored.Iterations < _iterations || stored.Hash.Length != HashBytes;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
