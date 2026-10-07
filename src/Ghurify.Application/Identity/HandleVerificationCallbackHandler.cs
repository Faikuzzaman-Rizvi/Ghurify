using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Applies the e-KYC provider's late answer to a pending check. Idempotent: the provider may
/// deliver the same callback more than once, and only the first one changes anything.
/// </summary>
public sealed class HandleVerificationCallbackHandler(
    IVerificationRepository verifications,
    IEkycProvider provider,
    ILogger<HandleVerificationCallbackHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(
        string? signature,
        string body,
        CancellationToken cancellationToken)
    {
        var callback = provider.ReadCallback(signature, body);

        if (callback is null)
        {
            logger.LogWarning("Rejected an e-KYC callback with a missing or invalid signature.");
            return AppError.Forbidden("The callback signature is not valid.");
        }

        var outcome = await verifications.SettleByProviderRefAsync(
            provider.Name, callback.ProviderRef, callback.Status, callback.Reason, cancellationToken);

        switch (outcome)
        {
            case VerificationWriteOutcome.NotFound:
                return AppError.NotFound("verification_not_found", "No check has that reference.");

            case VerificationWriteOutcome.NidInUse:
                logger.LogWarning("e-KYC approved {ProviderRef}, but its NID already verifies another account.", callback.ProviderRef);
                break;

            case VerificationWriteOutcome.AlreadySettled:
                logger.LogInformation("Ignored a repeated e-KYC callback for {ProviderRef}.", callback.ProviderRef);
                break;

            default:
                logger.LogInformation("e-KYC callback settled {ProviderRef}: {Status}.", callback.ProviderRef, callback.Status);
                break;
        }

        return Done.Value;
    }
}
