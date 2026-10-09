namespace Ghurify.Domain.Identity;

/// <summary>
/// What each built-in staff role may do when it is first created.
///
/// These sets match exactly what the matching role could do before admin roles became data, so
/// nobody gained or lost access when they did. They are the <em>starting</em> point only: once a
/// role exists, its permissions belong to whoever edits them in the portal, and neither a
/// redeploy nor this file changes them again.
///
/// The post-deployment script does the seeding, in SQL. This copy exists so tests can give a
/// fake actor the same access a real admin or safety-desk member has, and an integration test
/// asserts the two have not drifted apart.
/// </summary>
public static class StaffRoleDefaults
{
    /// <summary>Runs the platform day to day. Everything except editing the desk itself.</summary>
    public static readonly IReadOnlySet<string> Admin = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.DashboardView,
        Permissions.UsersView,
        Permissions.UsersSuspend,
        Permissions.UsersSecurity,
        Permissions.UsersRolesManage,
        Permissions.UsersVerify,
        Permissions.UsersDocumentsView,
        Permissions.UsersAvatarRemove,
        Permissions.TripsView,
        Permissions.TripsCancel,
        Permissions.DestinationsManage,
        Permissions.BookingsView,
        Permissions.PaymentsView,
        Permissions.PaymentsRefundRetry,
        Permissions.PayoutsView,
        Permissions.PayoutsApprove,
        Permissions.SafetySosView,
        Permissions.SafetySosManage,
        Permissions.SafetyCheckInsView,
        Permissions.SafetyDestinationsStatus,
        Permissions.SafetyPointsManage,
        Permissions.ModerationReportsView,
        Permissions.ModerationReportsResolve,
        Permissions.ModerationContentManage,
        Permissions.ModerationDisputes,
        Permissions.StaffView,
        Permissions.AuditView,
    };

    /// <summary>Reports, disputes and the stories people post.</summary>
    public static readonly IReadOnlySet<string> Moderator = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.DashboardView,
        Permissions.ModerationReportsView,
        Permissions.ModerationReportsResolve,
        Permissions.ModerationContentManage,
        Permissions.UsersAvatarRemove,
    };

    /// <summary>SOS, check-ins, destination closures and emergency points.</summary>
    public static readonly IReadOnlySet<string> SafetyDesk = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.DashboardView,
        Permissions.SafetySosView,
        Permissions.SafetySosManage,
        Permissions.SafetyCheckInsView,
        Permissions.SafetyDestinationsStatus,
        Permissions.SafetyPointsManage,
    };

    /// <summary>The starting permissions for a built-in role key, or null for the super admin
    /// (which holds every permission implicitly) and for anything else.</summary>
    public static IReadOnlySet<string>? For(string roleKey) => roleKey switch
    {
        StaffRoleKeys.Admin => Admin,
        StaffRoleKeys.Moderator => Moderator,
        StaffRoleKeys.SafetyDesk => SafetyDesk,
        _ => null,
    };
}
