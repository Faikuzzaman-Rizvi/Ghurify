using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// Re-checks the signed-in admin's own password and hands back a step-up receipt, which the
/// portal then sends with the few actions that demand it.
///
/// Rate-limited by the same per-address pause as sign-in, so a borrowed session cannot be turned
/// into somewhere to guess the owner's password quietly.
/// </summary>
public sealed class StartStepUpHandler(
    ICredentialRepository credentials,
    IPasswordHasher hasher,
    ISignInThrottle throttle,
    IStepUpTokens tokens,
    AccessService access,
    ILogger<StartStepUpHandler> logger)
{
    public async Task<Result<StepUpToken>> HandleAsync(
        long actorId,
        string? password,
        CancellationToken cancellationToken)
    {
        // Only staff can step up. Everyone else has nothing to step up to, and the password
        // check should not be reachable for them at all.
        if (!(await access.GetAsync(actorId, cancellationToken)).IsStaff)
        {
            return AppError.Forbidden();
        }

        var account = await credentials.FindByIdAsync(actorId, cancellationToken);
        if (account is null || !account.User.CanSignIn)
        {
            return AppError.Forbidden();
        }

        if (account.Password is null)
        {
            // An account with no password cannot prove anything this way. It must set one first.
            return AppError.Rule(
                "password_not_set",
                "Set a password on your account before using the admin desk.");
        }

        var email = account.User.Email;

        if (await throttle.PausedUntilAsync(email, cancellationToken) is not null)
        {
            return AppError.Rule(
                "sign_in_paused",
                "Too many wrong passwords. Wait a few minutes and try again.");
        }

        var given = password ?? string.Empty;
        if (given.Length is 0 or > PasswordPolicy.MaxLength || !hasher.Verify(given, account.Password))
        {
            await throttle.RegisterFailureAsync(email, cancellationToken);
            logger.LogWarning("Step-up refused for admin {ActorId}: wrong password.", actorId);
            return AppError.Validation("password_wrong", "That is not your password.");
        }

        await throttle.ClearAsync(email, cancellationToken);
        logger.LogInformation("Admin {ActorId} confirmed their password for a sensitive action.", actorId);

        return tokens.Issue(actorId);
    }
}
