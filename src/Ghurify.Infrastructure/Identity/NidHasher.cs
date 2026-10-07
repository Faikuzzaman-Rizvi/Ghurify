using System.Security.Cryptography;
using System.Text;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// HMAC-SHA256 of a national ID under a server-side pepper. A plain hash would be worthless:
/// there are only ten billion 10-digit numbers, so every one can be hashed in an afternoon.
/// Keyed, the stored hash is useless without the pepper, which never touches the database.
/// </summary>
public sealed class NidHasher(IOptions<VerificationOptions> options) : INidHasher
{
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(options.Value.NidPepper);

    public byte[] Hash(NationalId nationalId)
    {
        ArgumentNullException.ThrowIfNull(nationalId);
        return HMACSHA256.HashData(_pepper, Encoding.ASCII.GetBytes(nationalId.Digits));
    }

    /// <summary>
    /// The type is part of what is hashed, so a passport and a licence that happen to share a
    /// number never collide, and neither can collide with an NID (digits only, no prefix).
    /// </summary>
    public byte[] HashDocument(IdDocumentType type, string normalisedNumber)
    {
        ArgumentNullException.ThrowIfNull(normalisedNumber);
        return HMACSHA256.HashData(_pepper, Encoding.ASCII.GetBytes($"{type}:{normalisedNumber}"));
    }
}
