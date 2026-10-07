using Ghurify.Application.Safety;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Infrastructure.Sms;

/// <summary>
/// Development only: "sends" texts by writing them to the console, with the number masked, so the
/// SOS flow can be exercised without an SMS account. Registered only in Development.
/// </summary>
public sealed class DevelopmentSmsSender(ILogger<DevelopmentSmsSender> logger) : ISmsSender
{
    public Task<bool> SendAsync(PhoneNumber recipient, string text, CancellationToken cancellationToken)
    {
        logger.LogWarning("DEVELOPMENT SMS to {MaskedPhone}: {Text}", recipient.ToMasked(), text);
        return Task.FromResult(true);
    }
}
