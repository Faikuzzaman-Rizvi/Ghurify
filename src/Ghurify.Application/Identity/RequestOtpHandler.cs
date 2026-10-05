using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Sends a one-time sign-in code to an email address.
///
/// Note what this does not do: it never says whether the address already has an account.
/// Replying differently for known and unknown addresses would turn this endpoint into a way
/// to find out who is registered.
/// </summary>
public sealed class RequestOtpHandler(
    IOtpCodeRepository otpCodes,
    IOtpCodeService otpCodeService,
    IOtpSender otpSender,
    IClock clock,
    IOptions<IdentityOptions> options,
    ILogger<RequestOtpHandler> logger)
{
    private readonly IdentityOptions _options = options.Value;

    public async Task<IdentityResult<RequestOtpResult>> HandleAsync(
        RequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<RequestOtpResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;
        var now = clock.UtcNow;

        var code = otpCodeService.GenerateCode();
        var codeHash = otpCodeService.Hash(email, code);

        var outcome = await otpCodes.AddAsync(
            email,
            codeHash,
            expiresOn: now.AddMinutes(_options.OtpExpiryMinutes),
            windowStart: now.AddMinutes(-_options.OtpWindowMinutes),
            maxPerWindow: _options.OtpMaxPerWindow,
            cancellationToken);

        if (outcome == OtpSendOutcome.RateLimited)
        {
            // Masked: a full address in the logs is personal data, and this line is noisy.
            logger.LogWarning(
                "OTP rate limit reached for {MaskedEmail}; no code was sent.",
                email.ToMasked());

            return IdentityResult.Failure<RequestOtpResult>(IdentityError.RateLimited);
        }

        try
        {
            await otpSender.SendOtpAsync(email, code, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A misconfigured or unreachable mail server is an operational problem, not a bug.
            // The sender has already logged the detail; the caller gets a plain "could not
            // send" rather than a 500, because an unhandled exception here looks to the user
            // like the site is broken when the real answer is "try again shortly".
            logger.LogError(
                ex, "Could not deliver a sign-in code to {MaskedEmail}.", email.ToMasked());

            return IdentityResult.Failure<RequestOtpResult>(IdentityError.DeliveryFailed);
        }

        logger.LogInformation("OTP sent to {MaskedEmail}.", email.ToMasked());

        return IdentityResult.Success<RequestOtpResult>(new RequestOtpResult(
            ExpiresInSeconds: _options.OtpExpiryMinutes * 60,
            ResendAfterSeconds: _options.OtpResendAfterSeconds));
    }
}
