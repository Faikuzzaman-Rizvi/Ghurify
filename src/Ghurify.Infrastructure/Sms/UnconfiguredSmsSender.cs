using Ghurify.Application.Safety;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Infrastructure.Sms;

/// <summary>
/// Used outside Development until an SMS provider is chosen and configured. It does not pretend:
/// every text is reported as not sent and logged as an error, so an SOS still reaches the safety
/// desk and the host in the app while the gap is visible in monitoring.
/// </summary>
public sealed class UnconfiguredSmsSender(ILogger<UnconfiguredSmsSender> logger) : ISmsSender
{
    public Task<bool> SendAsync(PhoneNumber recipient, string text, CancellationToken cancellationToken)
    {
        logger.LogError("No SMS provider is configured; a text to {MaskedPhone} was not sent.", recipient.ToMasked());
        return Task.FromResult(false);
    }
}
