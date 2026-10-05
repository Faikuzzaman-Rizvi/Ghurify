using System.Diagnostics.CodeAnalysis;

namespace Ghurify.Domain.Identity;

/// <summary>
/// A Bangladeshi mobile number in E.164 form (<c>+8801XXXXXXXXX</c>).
///
/// People type their number in several shapes — <c>01712345678</c>, <c>8801712345678</c>,
/// <c>+880 1712-345678</c> — and all of them mean the same account. Normalising once, here,
/// is what stops the same person getting two accounts.
/// </summary>
public readonly record struct PhoneNumber
{
    private const string CountryCode = "880";

    /// <summary>Operator prefixes in use in Bangladesh: 013-019.</summary>
    private const string ValidSecondDigits = "3456789";

    private PhoneNumber(string value) => Value = value;

    /// <summary>The number in E.164, for example <c>+8801712345678</c>.</summary>
    public string Value { get; }

    public override string ToString() => Value;

    /// <summary>
    /// Normalises and validates a number as typed. Returns false rather than throwing:
    /// a badly typed number is an expected outcome, not an exceptional one.
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out PhoneNumber? phone)
    {
        phone = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Strip the separators people naturally type: spaces, dashes, brackets, dots.
        Span<char> digits = stackalloc char[input.Length];
        var length = 0;

        foreach (var character in input)
        {
            if (char.IsAsciiDigit(character))
            {
                digits[length++] = character;
            }
            else if (character is not (' ' or '-' or '(' or ')' or '.' or '+'))
            {
                return false;
            }
        }

        var national = new string(digits[..length]);

        // Accept 01712345678, 1712345678 and 8801712345678; reduce all to the national part.
        if (national.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            national = national[CountryCode.Length..];
        }

        if (national.StartsWith('0'))
        {
            national = national[1..];
        }

        // A Bangladeshi mobile number is 10 digits national: 1 + operator digit + 8 more.
        if (national.Length != 10 || national[0] != '1')
        {
            return false;
        }

        if (!ValidSecondDigits.Contains(national[1], StringComparison.Ordinal))
        {
            return false;
        }

        phone = new PhoneNumber($"+{CountryCode}{national}");
        return true;
    }

    /// <summary>
    /// Rebuilds a number already stored in E.164. Use only for values read back from the
    /// database, which the table's CHECK constraint has already validated.
    /// </summary>
    public static PhoneNumber FromStorage(string value) => new(value);

    /// <summary>
    /// The number with the middle hidden (<c>+88017****5678</c>), for anything a human reads.
    /// Full numbers must never reach logs.
    /// </summary>
    public string ToMasked() =>
        Value.Length <= 8 ? "***" : $"{Value[..7]}****{Value[^4..]}";
}
