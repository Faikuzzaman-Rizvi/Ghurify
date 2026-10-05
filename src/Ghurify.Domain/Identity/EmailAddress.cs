using System.Diagnostics.CodeAnalysis;

namespace Ghurify.Domain.Identity;

/// <summary>
/// An email address, normalised to lower case with surrounding spaces removed.
///
/// Normalising once, here, is what stops one person ending up with two accounts because they
/// typed <c>Rizvi@Example.com</c> on one visit and <c>rizvi@example.com</c> on the next. The
/// domain part of an address is case-insensitive by specification; the local part technically
/// is not, but no mail provider people actually use treats it as significant, and treating it
/// as significant here would create duplicate accounts for the same inbox.
/// </summary>
public readonly record struct EmailAddress
{
    /// <summary>
    /// The column is NVARCHAR(256), and 254 is the longest address that can be delivered.
    /// </summary>
    public const int MaxLength = 254;

    private EmailAddress(string value) => Value = value;

    /// <summary>The normalised address, for example <c>rizvi@example.com</c>.</summary>
    public string Value { get; }

    public override string ToString() => Value;

    /// <summary>
    /// Normalises and validates an address as typed. Returns false rather than throwing: a
    /// mistyped address is an expected outcome, not an exceptional one.
    ///
    /// The check is deliberately shallow. Fully validating an address against the RFC is
    /// famously impractical, and the only proof an address works is that a code sent to it
    /// comes back — which is exactly what the sign-in flow does.
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out EmailAddress? email)
    {
        email = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        if (trimmed.Length > MaxLength)
        {
            return false;
        }

        // Exactly one @, with something before it and a dotted domain after it.
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return false;
        }

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];

        if (!IsValidDomain(domain) || local.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        email = new EmailAddress(trimmed.ToLowerInvariant());
        return true;
    }

    /// <summary>
    /// Rebuilds an address already stored. Use only for values read back from the database,
    /// which the table's CHECK constraint has already validated.
    /// </summary>
    public static EmailAddress FromStorage(string value) => new(value);

    /// <summary>
    /// The address with the middle of the local part hidden (<c>ri****i@example.com</c>), for
    /// anything a human reads. Full addresses must never reach logs: they identify a person.
    /// </summary>
    public string ToMasked()
    {
        var at = Value.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0)
        {
            return "***";
        }

        var local = Value[..at];
        var domain = Value[at..];

        // Too short to hide anything meaningfully; mask the lot.
        if (local.Length <= 2)
        {
            return $"***{domain}";
        }

        return $"{local[0]}****{local[^1]}{domain}";
    }

    private static bool IsValidDomain(string domain)
    {
        if (domain.Length < 3 || domain.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        var dot = domain.LastIndexOf('.');

        // Needs a dot that is neither first nor last, and a TLD of at least two characters.
        return dot > 0
            && dot < domain.Length - 2
            && !domain.StartsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal);
    }
}
