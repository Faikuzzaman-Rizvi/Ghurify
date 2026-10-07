using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Emailed one-time codes: issuing one (rate-limited per inbox) and checking one (limited
/// guesses, single use, bound to its purpose). Only the keyed hash of a code is ever stored.
/// </summary>
public sealed class EmailCodes(
    IOtpCodeRepository otpCodes,
    IOtpCodeService otpCodeService,
    IOtpSender sender,
    IClock clock,
    IOptions<IdentityOptions> options,
    ILogger<EmailCodes> logger)
{
    private readonly IdentityOptions _options = options.Value;

    /// <summary>
    /// Stores a new code and, when <paramref name="send"/> is true, emails it. With send false
    /// the code is stored and never sent: the rate limit and the time taken are then the same
    /// for addresses with and without an account, and nobody can ever use that code.
    /// </summary>
    public async Task<IdentityResult<CodeSentResult>> IssueAsync(
        EmailAddress email,
        OtpPurpose purpose,
        bool send,
        CancellationToken cancellationToken)
    {

        var now = clock.UtcNow;
        var code = otpCodeService.GenerateCode();

        var outcome = await otpCodes.AddAsync(
            email,
            purpose,
            otpCodeService.Hash(email, code),
            expiresOn: now.AddMinutes(_options.OtpExpiryMinutes),
            windowStart: now.AddMinutes(-_options.OtpWindowMinutes),
            maxPerWindow: _options.OtpMaxPerWindow,
            cancellationToken);

        if (outcome == OtpSendOutcome.RateLimited)
        {
            logger.LogWarning("Code limit reached for {MaskedEmail}; nothing was sent.", email.ToMasked());
            return IdentityResult.Failure<CodeSentResult>(IdentityError.RateLimited);
        }

        if (send)
        {
            try
            {
                await sender.SendOtpAsync(email, code, purpose, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The sender logged the SMTP detail. The caller gets "could not send, try again",
                // not a 500 that looks like the site is broken.
                logger.LogError(ex, "Could not email a {Purpose} code to {MaskedEmail}.", purpose, email.ToMasked());
                return IdentityResult.Failure<CodeSentResult>(IdentityError.DeliveryFailed);
            }
        }

        return IdentityResult.Success(new CodeSentResult(_options.OtpExpiryMinutes * 60, _options.OtpResendAfterSeconds));
    }

    /// <summary>
    /// Checks a code and uses it up. False for every failure alike (none, wrong, expired, used,
    /// locked); the reason goes to the log, never to the caller.
    /// </summary>
    public async Task<bool> RedeemAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken)
    {

        var now = clock.UtcNow;
        var otp = await otpCodes.FindLatestAsync(email, purpose, cancellationToken);

        if (otp is null)
        {
            return Reject(email, purpose, OtpRejection.NotFound);
        }

        var rejection = otp.CanAttempt(now);
        if (rejection != OtpRejection.None)
        {
            return Reject(email, purpose, rejection);
        }

        if (!otpCodeService.Matches(email, code, otp.CodeHash))
        {
            // Counted in the database, so simultaneous guesses cannot slip past the limit.
            var attempt = await otpCodes.RegisterFailedAttemptAsync(otp.Id, _options.OtpMaxAttempts, cancellationToken);
            if (attempt.IsLocked)
            {
                logger.LogWarning("Code locked for {MaskedEmail} after {Attempts} wrong attempts.", email.ToMasked(), attempt.Attempts);
            }

            return Reject(email, purpose, OtpRejection.WrongCode);
        }

        await otpCodes.ConsumeAsync(otp.Id, now, cancellationToken);
        return true;
    }

    /// <summary>Tells the owner of an address something about their account. Never throws.</summary>
    public async Task NotifyAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken)
    {

        try
        {
            await sender.SendNoticeAsync(email, notice, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A notice is a courtesy; failing to send it must not fail what the person was doing.
            logger.LogError(ex, "Could not email the {Notice} notice to {MaskedEmail}.", notice, email.ToMasked());
        }
    }

    private bool Reject(EmailAddress email, OtpPurpose purpose, OtpRejection reason)
    {
        logger.LogInformation("{Purpose} code refused for {MaskedEmail}: {Reason}.", purpose, email.ToMasked(), reason);
        return false;
    }
}
