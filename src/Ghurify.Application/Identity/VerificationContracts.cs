using System.ComponentModel.DataAnnotations;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>
/// Ask for an identity check: the ID it is based on and its number (typed as on the card), date
/// of birth, and the photos already uploaded (both sides of the ID, or the passport photo page;
/// and the selfie for the NID + selfie level).
/// </summary>
public sealed record StartVerificationCommand(
    VerificationLevel Level,
    string IdNumber,
    DateOnly DateOfBirth,
    IReadOnlyList<long> DocumentIds,
    IdDocumentType IdType = IdDocumentType.Nid);

/// <summary>An admin's decision on a pending check. A rejection must say why.</summary>
public sealed record ReviewVerificationCommand(bool Approve, string? Reason);

/// <summary>
/// Secrets for identity verification. Validated at startup: without the pepper, NID hashes would
/// be a plain SHA of a 10-digit number, which anyone can reverse by trying every number.
/// </summary>
public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Verification:NidPepper is required.")]
    [MinLength(32, ErrorMessage = "Verification:NidPepper must be at least 32 characters.")]
    public string NidPepper { get; set; } = string.Empty;

    /// <summary>Shared secret the e-KYC provider signs its callbacks with.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Verification:CallbackSecret is required.")]
    [MinLength(32, ErrorMessage = "Verification:CallbackSecret must be at least 32 characters.")]
    public string CallbackSecret { get; set; } = string.Empty;
}
