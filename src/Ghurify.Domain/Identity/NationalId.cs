using System.Diagnostics.CodeAnalysis;

namespace Ghurify.Domain.Identity;

/// <summary>
/// A Bangladeshi national ID number: 10 digits (smart card), 13 or 17 digits (older laminated
/// cards). It is only ever held in memory long enough to hash it and send it to the e-KYC
/// provider; it is never stored and never logged. <see cref="ToString"/> is masked so an
/// accidental log line or exception message cannot leak it.
/// </summary>
public sealed class NationalId
{
    private NationalId(string digits) => Digits = digits;

    /// <summary>The bare digits. Pass to the hasher and the provider only.</summary>
    public string Digits { get; }

    public static bool TryParse(string? input, [NotNullWhen(true)] out NationalId? nationalId)
    {
        nationalId = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // People copy the number with spaces or dashes; nothing else is allowed.
        var digits = new string([.. input.Where(character => character is not (' ' or '-'))]);

        if (digits.Length is not (10 or 13 or 17) || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        nationalId = new NationalId(digits);
        return true;
    }

    /// <summary>Last three digits only, e.g. <c>*******123</c>.</summary>
    public override string ToString() => new string('*', Digits.Length - 3) + Digits[^3..];
}
