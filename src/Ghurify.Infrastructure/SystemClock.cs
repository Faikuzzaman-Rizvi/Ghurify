using Ghurify.Application.Identity;

namespace Ghurify.Infrastructure;

/// <summary>
/// The real clock. Tests substitute their own so OTP expiry and token lifetimes can be
/// exercised without waiting for wall-clock time to pass.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
