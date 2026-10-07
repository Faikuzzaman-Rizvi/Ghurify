namespace Ghurify.Application.Identity;

/// <summary>The signed-in user's own identity checks, newest first.</summary>
public sealed class GetMyVerificationsHandler(IVerificationRepository verifications)
{
    public Task<IReadOnlyList<VerificationRecord>> HandleAsync(long userId, CancellationToken cancellationToken) =>
        verifications.QueryForUserAsync(userId, cancellationToken);
}
