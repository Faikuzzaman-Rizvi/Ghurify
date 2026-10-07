using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>The signed-in user's profile, as they edit it.</summary>
public sealed class GetProfileHandler(IProfileRepository profiles)
{
    public async Task<Result<ProfileDetails>> HandleAsync(long userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userId, cancellationToken);

        return profile is null
            ? AppError.NotFound("profile_not_found", "There is no profile for this account.")
            : profile;
    }
}
