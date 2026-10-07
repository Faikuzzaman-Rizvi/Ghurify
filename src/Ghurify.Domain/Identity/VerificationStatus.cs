namespace Ghurify.Domain.Identity;

/// <summary>Where an identity check stands. Stored as TINYINT in [Main].[Verification].</summary>
public enum VerificationStatus : byte
{
    /// <summary>With the provider, or waiting for a person on the verification desk.</summary>
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}
