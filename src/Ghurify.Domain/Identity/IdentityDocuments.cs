using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Ghurify.Domain.Identity;

/// <summary>The document an identity check is based on. Stored as TINYINT in [Main].[Verification].</summary>
public enum IdDocumentType : byte
{
    Nid = 1,
    Passport = 2,
    DrivingLicence = 3,
}

/// <summary>What a photo uploaded for verification shows. Stored as TINYINT.</summary>
public enum VerificationDocumentKind : byte
{
    NidFront = 1,
    NidBack = 2,
    PassportPhotoPage = 3,
    DrivingLicenceFront = 4,
    DrivingLicenceBack = 5,

    /// <summary>The person holding the same ID next to their face.</summary>
    Selfie = 6,

    /// <summary>A guide or tour operator licence. Optional, shown to the reviewer.</summary>
    ProfessionalLicence = 7,
}

public enum VerificationDocumentStatus : byte
{
    AwaitingUpload = 1,
    Ready = 2,
    Rejected = 3,
    Purged = 4,
}

/// <summary>Which photos a check needs, so a reviewer always has what they need to decide.</summary>
public static class VerificationRequirements
{
    /// <summary>The photos of the ID document itself, by type.</summary>
    public static IReadOnlyList<VerificationDocumentKind> For(IdDocumentType type) => type switch
    {
        IdDocumentType.Passport => [VerificationDocumentKind.PassportPhotoPage],
        IdDocumentType.DrivingLicence => [VerificationDocumentKind.DrivingLicenceFront, VerificationDocumentKind.DrivingLicenceBack],
        _ => [VerificationDocumentKind.NidFront, VerificationDocumentKind.NidBack],
    };

    /// <summary>
    /// The photos still missing for a check of this level on this ID, in a fixed order. The
    /// selfie is needed for the NID + selfie level (the one hosts need).
    /// </summary>
    public static IReadOnlyList<VerificationDocumentKind> Missing(
        IdDocumentType type,
        VerificationLevel level,
        IReadOnlyCollection<VerificationDocumentKind> provided)
    {
        ArgumentNullException.ThrowIfNull(provided);

        var needed = For(type).ToList();
        if (level >= VerificationLevel.NidSelfie)
        {
            needed.Add(VerificationDocumentKind.Selfie);
        }

        return [.. needed.Where(kind => !provided.Contains(kind))];
    }

    /// <summary>Whether a photo of this kind belongs with a check on this ID at all.</summary>
    public static bool Fits(IdDocumentType type, VerificationDocumentKind kind) =>
        kind is VerificationDocumentKind.Selfie or VerificationDocumentKind.ProfessionalLicence || For(type).Contains(kind);
}

/// <summary>
/// The number on a passport or driving licence, normalised: upper case, letters and digits only.
/// (A national ID number is a <see cref="NationalId"/>.) Never stored, only hashed.
/// </summary>
public static class DocumentNumber
{
    public static bool TryNormalise(IdDocumentType type, string? input, [NotNullWhen(true)] out string? normalised)
    {
        normalised = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var builder = new StringBuilder(input.Length);
        foreach (var character in input.Trim())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
            else if (character is not (' ' or '-' or '/'))
            {
                return false;
            }
        }

        var value = builder.ToString();
        var valid = type switch
        {
            // Bangladeshi passports: two letters and seven digits (A1234567 on older ones).
            IdDocumentType.Passport => value.Length is >= 7 and <= 12,
            // Driving licence numbers mix letters and digits, e.g. DK0123456C00001.
            IdDocumentType.DrivingLicence => value.Length is >= 8 and <= 20,
            _ => false,
        };

        if (!valid || !value.Any(char.IsAsciiDigit))
        {
            return false;
        }

        normalised = value;
        return true;
    }
}
