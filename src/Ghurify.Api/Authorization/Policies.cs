using Ghurify.Application.Identity;
using Ghurify.Api.Endpoints;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Ghurify.Api.Authorization;

/// <summary>
/// Named policies. Endpoints use these rather than role strings, and every policy is evaluated
/// against the database (through <see cref="AccessService"/>), not against claims in the token:
/// a suspension or a revoked role takes effect on the next request, not 15 minutes later.
///
/// Policies are the first gate only. Each use case checks again, and SQL filters by owner.
/// </summary>
public static class Policies
{
    /// <summary>National ID checked. Required to request to join a trip.</summary>
    public const string VerifiedTraveler = nameof(VerifiedTraveler);

    /// <summary>Holds the Host role and passed the selfie check. Required to publish trips.</summary>
    public const string VerifiedHost = nameof(VerifiedHost);

    /// <summary>Holds the Host role, verified or not: may draft trips and see their own.</summary>
    public const string Host = nameof(Host);

    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>The safety desk (or an admin): SOS board, check-ins, destination alerts.</summary>
    public const string SafetyDesk = nameof(SafetyDesk);

    /// <summary>Moderators (or an admin): reports and content.</summary>
    public const string Moderator = nameof(Moderator);

    /// <summary>Any staff role: what the admin area needs to open at all.</summary>
    public const string Staff = nameof(Staff);

    public static void AddGhurifyPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // [Authorize] by default: an endpoint that forgets to say otherwise is closed, not open.
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        Add(options, VerifiedTraveler, access => access.IsVerifiedTraveler);
        Add(options, VerifiedHost, access => access.IsVerifiedHost);
        Add(options, Host, access => access.IsActive && access.Has(Role.Host));
        Add(options, AdminOnly, access => access.IsAdmin);
        Add(options, SafetyDesk, access => access.IsSafetyDesk);
        Add(options, Moderator, access => access.IsModerator);
        Add(options, Staff, access => access.IsAdmin || access.IsSafetyDesk || access.IsModerator);
    }

    private static void Add(AuthorizationOptions options, string name, Func<UserAccess, bool> rule) =>
        options.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AccessRequirement(rule)));
}

/// <summary>A rule over the caller's current <see cref="UserAccess"/>.</summary>
public sealed class AccessRequirement(Func<UserAccess, bool> rule) : IAuthorizationRequirement
{
    public bool IsMet(UserAccess access) => rule(access);
}

/// <summary>Loads the caller's access once per request and applies the requirement to it.</summary>
public sealed class AccessRequirementHandler(AccessService access) : AuthorizationHandler<AccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.FindUserId() is not { } userId)
        {
            return;
        }

        var current = await access.GetAsync(userId, CancellationToken.None);

        if (requirement.IsMet(current))
        {
            context.Succeed(requirement);
        }
    }
}
