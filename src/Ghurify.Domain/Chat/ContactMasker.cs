using System.Text.RegularExpressions;

namespace Ghurify.Domain.Chat;

/// <summary>
/// Hides phone and mobile-wallet numbers (bKash, Nagad, Rocket all use mobile numbers) in chat
/// messages while anyone in the group has not paid yet. "Pay me on bKash instead" is how travellers
/// get talked out of escrow, and escrow is the safety promise.
///
/// A number is any run of 10 or more digits, Latin or Bangla, optionally starting with + and
/// broken up by spaces, dashes, dots or brackets: 01712-345678, +880 1712 345678, ০১৭১২৩৪৫৬৭৮.
/// Ten is deliberate: Bangladeshi mobile numbers have 10 national digits, while dates
/// (2026-10-14) and prices have fewer, so ordinary messages are left alone.
/// </summary>
public static partial class ContactMasker
{
    /// <summary>
    /// What a number becomes. No words: the web app explains it in the reader's language, using the
    /// message's WasMasked flag.
    /// </summary>
    public const string Mask = "••••••••";

    public static (string Text, bool WasMasked) Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var masked = false;
        var result = NumberPattern().Replace(text, match =>
        {
            var digits = match.Value.Count(IsDigit);
            if (digits < 10)
            {
                return match.Value;
            }

            masked = true;
            return Mask;
        });

        return (result, masked);
    }

    /// <summary>True when the text holds something that would be masked.</summary>
    public static bool ContainsContact(string text) => Apply(text).WasMasked;

    private static bool IsDigit(char character) =>
        char.IsAsciiDigit(character) || character is >= '০' and <= '৯';

    [GeneratedRegex(@"\+?[0-9০-৯](?:[\s\-.()]{0,2}[0-9০-৯]){7,}")]
    private static partial Regex NumberPattern();
}
