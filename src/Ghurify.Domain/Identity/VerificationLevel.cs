namespace Ghurify.Domain.Identity;

/// <summary>
/// How strongly a person's identity has been checked. Higher is stronger; an approved level
/// implies every level below it. Stored as TINYINT in [Main].[Verification].
/// </summary>
public enum VerificationLevel : byte
{
    /// <summary>Phone number confirmed. Reserved: needs an SMS provider, which is not wired yet.</summary>
    Phone = 1,

    /// <summary>National ID checked against the national register by the e-KYC provider.</summary>
    Nid = 2,

    /// <summary>National ID checked and a live selfie matched to its photo.</summary>
    NidSelfie = 3,
}
