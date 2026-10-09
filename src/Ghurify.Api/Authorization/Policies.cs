using Ghurify.Application.Identity;
using Ghurify.Api.Endpoints;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Ghurify.Api.Authorization;

/// <summary>
/// Named policies. Endpoints use these rather than role strings, and every policy is evaluated
/// against the database (through <see cref="AccessService"/>), not against claims in the token:
/// a suspension, a revoked role or a permission a super admin took away this minute takes
/// effect on the next request, not 15 minutes later.
///
/// Admin endpoints do not name a role at all. They name the one permission they need, with
/// <see cref="Policies.Require"/>, and a super admin can then move that permission between roles
/// from the portal without anybody editing this file.
///
/// Policies are the first gate only. Each use case checks the same permission again, and SQL
/// filters by owner.
/// </summary>
public static class Policies
{
    /// <summary>National ID checked. Required to request to join a trip.</summary>
    public const string VerifiedTraveler = nameof(VerifiedTraveler);

    /// <summary>Holds the Host role and passed the selfie check. Required to publish trips.</summary>
    public const string VerifiedHost = nameof(VerifiedHost);

    /// <summary>Holds the Host role, verified or not: may draft trips and see their own.</summary>
    public const string Host = nameof(Host);

    /// <summary>
    /// Holds any staff role: what the admin portal needs to open at all. Which sections then
    /// appear is a permission each, so this is the door and not the keys.
    /// </summary>
    public const string Staff = nameof(Staff);

    /// <summary>Prefix of the generated per-permission policies. See <see cref="Require"/>.</summary>
    internal const string PermissionPrefix = "perm:";

    /// <summary>
    /// The policy name for one permission, e.g. <c>Policies.Require(Permissions.PayoutsApprove)</c>.
    /// <see cref="PermissionPolicyProvider"/> builds these on demand, so there is no list of
    /// policies to keep in step with the list of permissions.
    /// </summary>
    public static string Require(string permission) => PermissionPrefix + permission;

    public static void AddGhurifyPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // [Authorize] by default: an endpoint that forgets to say otherwise is closed, not open.
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        Add(options, VerifiedTraveler, access => access.IsVerifiedTraveler);
        Add(options, VerifiedHost, access => access.IsVerifiedHost);
        Add(options, Host, access => access.IsActive && access.Has(Role.Host));
        Add(options, Staff, access => access.IsStaff);
    }

    internal static AuthorizationPolicy ForPermission(string permission) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new AccessRequirement(access => access.Can(permission)))
            .Build();

    private static void Add(AuthorizationOptions options, string name, Func<UserAccess, bool> rule) =>
        options.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AccessRequirement(rule)));
}

/// <summary>
/// Hands out a policy for every <c>perm:...</c> name, so an endpoint can ask for a permission
/// without one being registered up front. Anything else falls through to the policies registered
/// in <see cref="Policies.AddGhurifyPolicies"/>.
/// </summary>
public sealed class PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(Policies.PermissionPrefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permission = policyName[Policies.PermissionPrefix.Length..];

        // A typo would otherwise become a policy nobody can ever satisfy, which looks like a
        // mysterious 403 at runtime. Refusing to build it surfaces the mistake as a startup-time
        // failure on the first request to that route instead.
        return PermissionCatalog.Exists(permission)
            ? Task.FromResult<AuthorizationPolicy?>(Policies.ForPermission(permission))
            : throw new InvalidOperationException(
                $"\"{permission}\" is not a permission. Add it to Ghurify.Domain.Identity.Permissions and PermissionCatalog.");
    }
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
