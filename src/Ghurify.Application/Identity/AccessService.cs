using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>
/// Answers "what may this user do?" for the current request. Scoped, and caches its answer, so
/// the authorization policy and the use case behind it share one database read.
/// </summary>
public sealed class AccessService(IUserAccessRepository repository)
{
    private readonly Dictionary<long, UserAccess> _cache = [];

    /// <summary>The user's access; an inactive, role-less access when the account is gone.</summary>
    public async Task<UserAccess> GetAsync(long userId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var access = await repository.GetAsync(userId, cancellationToken) ?? UserAccess.None(userId);
        _cache[userId] = access;
        return access;
    }

    /// <summary>Drops the cached answer after this request changed it (a new role, a verification).</summary>
    public void Forget(long userId) => _cache.Remove(userId);
}
