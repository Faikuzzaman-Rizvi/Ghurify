using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Starts an identity check: the ID it is based on (national ID, passport or driving licence),
/// its number, and the photos already uploaded. The photos go to the reviewer with the check.
///
/// The number is hashed, then dropped: it is never stored and never logged. A national ID is also
/// handed to the e-KYC provider (in production, until a real provider is contracted, that is
/// manual review); a passport or licence always goes to manual review. A pending check is settled
/// from the admin queue, or by the provider's signed callback.
/// </summary>
public sealed class StartVerificationHandler(
    IVerificationRepository verifications,
    IVerificationDocumentRepository documents,
    IProfileRepository profiles,
    IEkycProvider provider,
    INidHasher hasher,
    AccessService access,
    ILogger<StartVerificationHandler> logger)
{
    /// <summary>Stored as the provider of checks no provider can make: a person decides them.</summary>
    public const string ManualReview = "manual";

    public async Task<Result<VerificationRecord>> HandleAsync(
        long userId,
        StartVerificationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Level == VerificationLevel.Phone)
        {
            return AppError.Validation(
                "phone_verification_unavailable",
                "Phone verification is not available yet. Choose an ID check.");
        }

        if (!Enum.IsDefined(command.IdType) || !Enum.IsDefined(command.Level))
        {
            return AppError.Validation("invalid_id_type", "Choose a national ID, passport or driving licence.");
        }

        NationalId? nationalId = null;
        byte[] idHash;
        if (command.IdType == IdDocumentType.Nid)
        {
            if (!NationalId.TryParse(command.IdNumber, out nationalId))
            {
                return AppError.Validation("invalid_nid", "Enter the 10, 13 or 17 digit number from your national ID card.");
            }

            idHash = hasher.Hash(nationalId);
        }
        else if (DocumentNumber.TryNormalise(command.IdType, command.IdNumber, out var number))
        {
            idHash = hasher.HashDocument(command.IdType, number);
        }
        else
        {
            return AppError.Validation("invalid_document_number", "Enter the number exactly as it appears on the document.");
        }

        var current = await access.GetAsync(userId, cancellationToken);
        if (!current.IsActive)
        {
            return AppError.Forbidden();
        }

        if (current.IsVerifiedAtLeast(command.Level))
        {
            return AppError.Conflict("already_verified", "You are already verified at this level.");
        }

        var existing = await verifications.QueryForUserAsync(userId, cancellationToken);
        if (existing.Any(check => check.Status == VerificationStatus.Pending))
        {
            return AppError.Conflict("verification_pending", "A check is already in progress. We will let you know the result.");
        }

        var profile = await profiles.GetAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(profile?.DisplayName) || profile.Gender is null)
        {
            // The register is matched on name, and gender gates women-only trips, so both must be
            // on the profile before the check that fixes them.
            return AppError.Rule("profile_incomplete", "Add your full name and gender to your profile before verifying.");
        }

        var documentCheck = await CheckDocumentsAsync(userId, command, cancellationToken);
        if (documentCheck is not null)
        {
            return documentCheck;
        }

        var result = nationalId is not null
            ? await provider.StartAsync(new EkycRequest(nationalId, command.DateOfBirth, command.Level, profile.DisplayName), cancellationToken)
            : new EkycResult(ManualReview + "-" + Guid.NewGuid().ToString("N"), VerificationStatus.Pending, null);
        var providerName = nationalId is not null ? provider.Name : ManualReview;

        var outcome = await verifications.AddAsync(
            new NewVerification(
                userId,
                command.Level,
                result.Status,
                idHash,
                providerName,
                result.ProviderRef,
                result.Reason,
                command.IdType,
                [.. command.DocumentIds.Distinct()]),
            cancellationToken);

        switch (outcome)
        {
            case VerificationWriteOutcome.NidInUse:
                logger.LogWarning("User {UserId} presented an ID already verifying another account.", userId);
                return AppError.Conflict("nid_in_use", "This ID is already verified on another account.");
            case VerificationWriteOutcome.DocumentsNotUsable:
                return AppError.Rule("documents_not_ready", "Upload the photos again, then submit.");
        }

        access.Forget(userId);
        logger.LogInformation(
            "User {UserId} started a {Level} check on a {IdType} with {Provider}: {Status}.",
            userId, command.Level, command.IdType, providerName, result.Status);

        var saved = await verifications.QueryForUserAsync(userId, cancellationToken);
        return saved[0];
    }

    /// <summary>Every photo named must be the user's own and ready, fit this ID, and none may be missing.</summary>
    private async Task<AppError?> CheckDocumentsAsync(long userId, StartVerificationCommand command, CancellationToken cancellationToken)
    {
        var requested = (command.DocumentIds ?? []).Distinct().ToList();
        var uploaded = await documents.QueryUnsubmittedAsync(userId, cancellationToken);
        var chosen = uploaded.Where(document => requested.Contains(document.Id)).ToList();

        if (chosen.Count != requested.Count || chosen.Any(document => document.Status != VerificationDocumentStatus.Ready))
        {
            return AppError.Rule("documents_not_ready", "Upload the photos again, then submit.");
        }

        if (chosen.Any(document => !VerificationRequirements.Fits(command.IdType, document.Kind)))
        {
            return AppError.Validation("documents_wrong_kind", "Some photos are for a different kind of ID.");
        }

        var missing = VerificationRequirements.Missing(command.IdType, command.Level, [.. chosen.Select(document => document.Kind)]);
        if (missing.Count > 0)
        {
            return AppError.Validation("documents_missing", "Add a photo of: " + string.Join(", ", missing) + ".");
        }

        return null;
    }
}
