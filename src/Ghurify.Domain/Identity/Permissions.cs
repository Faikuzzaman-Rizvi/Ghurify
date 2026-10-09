namespace Ghurify.Domain.Identity;

/// <summary>
/// One thing a staff member may do on the admin desk. Permissions are the unit of authority:
/// endpoints and use cases ask for a permission, never for a role name, so a role is only ever
/// a named bundle of these.
///
/// The keys are stable strings, stored in <c>[Main].[StaffRolePermission].[Permission]</c> and
/// written into the audit log. A string rather than a TINYINT because the list grows with every
/// admin screen, and because <c>payouts.approve</c> in an audit row needs no lookup table to
/// read. Every key is at most 40 characters (the column width) and names its group first, so
/// related permissions sort together.
///
/// Adding one: add the constant, list it in <see cref="PermissionCatalog"/>, add the English and
/// Bangla label in the web app's i18n files, and grant it to the system roles that should have
/// it in a DbUp data script. A permission the database holds but code does not know is ignored
/// on read and refused on write, so a downgrade cannot widen anyone's access.
/// </summary>
public static class Permissions
{
    // --- Overview ---

    /// <summary>Open the admin portal and see the overview counts.</summary>
    public const string DashboardView = "dashboard.view";

    // --- People ---

    /// <summary>Search people and open one person's record.</summary>
    public const string UsersView = "users.view";

    /// <summary>Suspend, reactivate or close an account.</summary>
    public const string UsersSuspend = "users.suspend";

    /// <summary>Force a password reset, ending every session.</summary>
    public const string UsersSecurity = "users.security";

    /// <summary>Grant or revoke the platform roles (host, guide, creator...).</summary>
    public const string UsersRolesManage = "users.roles.manage";

    /// <summary>Work the identity-check queue: approve or reject.</summary>
    public const string UsersVerify = "users.verify";

    /// <summary>
    /// Open the ID photos behind a check. Separate from <see cref="UsersVerify"/> because these
    /// are national ID images: the narrowest possible group should hold it, and every viewing is
    /// written to the audit log.
    /// </summary>
    public const string UsersDocumentsView = "users.documents.view";

    /// <summary>Remove someone's profile picture.</summary>
    public const string UsersAvatarRemove = "users.avatar.remove";

    // --- Trips ---

    /// <summary>See every trip, in every status.</summary>
    public const string TripsView = "trips.view";

    /// <summary>Cancel a trip, refunding its travellers.</summary>
    public const string TripsCancel = "trips.cancel";

    /// <summary>Add and edit destinations.</summary>
    public const string DestinationsManage = "destinations.manage";

    // --- Money ---

    /// <summary>Look up a booking and its escrow position.</summary>
    public const string BookingsView = "bookings.view";

    /// <summary>See payments and their gateway history.</summary>
    public const string PaymentsView = "payments.view";

    /// <summary>Push stuck refunds back to the gateway.</summary>
    public const string PaymentsRefundRetry = "payments.refund.retry";

    /// <summary>See the payout queue.</summary>
    public const string PayoutsView = "payouts.view";

    /// <summary>Confirm a payout has been sent to the host.</summary>
    public const string PayoutsApprove = "payouts.approve";

    // --- Safety ---

    /// <summary>Watch the live SOS board.</summary>
    public const string SafetySosView = "safety.sos.view";

    /// <summary>Acknowledge and resolve an SOS.</summary>
    public const string SafetySosManage = "safety.sos.manage";

    /// <summary>See check-ins that were missed.</summary>
    public const string SafetyCheckInsView = "safety.checkins.view";

    /// <summary>Put a destination on caution or close it.</summary>
    public const string SafetyDestinationsStatus = "safety.destinations.status";

    /// <summary>Maintain the police stations and hospitals behind "nearest help".</summary>
    public const string SafetyPointsManage = "safety.points.manage";

    // --- Moderation ---

    /// <summary>See the reports queue.</summary>
    public const string ModerationReportsView = "moderation.reports.view";

    /// <summary>Decide a report.</summary>
    public const string ModerationReportsResolve = "moderation.reports.resolve";

    /// <summary>See every story and hide any of them.</summary>
    public const string ModerationContentManage = "moderation.content.manage";

    /// <summary>
    /// Decide a money dispute. Separate from an ordinary report because the outcome moves money.
    /// </summary>
    public const string ModerationDisputes = "moderation.disputes";

    // --- Staff ---

    /// <summary>See who is on the admin desk and which roles exist.</summary>
    public const string StaffView = "staff.view";

    /// <summary>Put someone on the admin desk, or take them off it.</summary>
    public const string StaffAssign = "staff.assign";

    /// <summary>Create admin roles and choose what each one may do.</summary>
    public const string StaffRolesManage = "staff.roles.manage";

    // --- The site itself ---

    /// <summary>
    /// Change the site's name, tagline, description, contact details, social links and images.
    /// </summary>
    public const string SettingsBranding = "settings.branding";

    /// <summary>
    /// Change the colours and fonts. Separate from branding because a bad theme can make every
    /// page unreadable, including the one used to put it right.
    /// </summary>
    public const string SettingsTheme = "settings.theme";

    // --- System ---

    /// <summary>Read the audit log.</summary>
    public const string AuditView = "audit.view";
}

/// <summary>Which part of the portal a permission belongs to. Decides how the role editor groups them.</summary>
public enum PermissionGroup
{
    Overview = 1,
    People = 2,
    Trips = 3,
    Money = 4,
    Safety = 5,
    Moderation = 6,
    Staff = 7,

    /// <summary>How the site looks and what it says about itself.</summary>
    Site = 8,

    System = 9,
}

/// <summary>
/// A permission as the role editor sees it. No label: the web app translates
/// <see cref="Key"/> through its i18n files, so admin screens read in Bangla too.
/// </summary>
/// <param name="RequiresStepUp">
/// Using it makes the caller re-enter their password first. Reserved for the permissions that
/// could be used to take over the platform, where a borrowed, still-valid access token must not
/// be enough on its own.
/// </param>
public sealed record PermissionDefinition(string Key, PermissionGroup Group, bool RequiresStepUp);

/// <summary>
/// Every permission the code knows about. The one source of truth: the role editor lists these,
/// and nothing else can be stored.
/// </summary>
public static class PermissionCatalog
{
    /// <summary>In portal order, grouped, so the role editor can render it straight through.</summary>
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Permissions.DashboardView, PermissionGroup.Overview, false),

        new(Permissions.UsersView, PermissionGroup.People, false),
        new(Permissions.UsersSuspend, PermissionGroup.People, false),
        new(Permissions.UsersSecurity, PermissionGroup.People, false),
        new(Permissions.UsersRolesManage, PermissionGroup.People, false),
        new(Permissions.UsersVerify, PermissionGroup.People, false),
        new(Permissions.UsersDocumentsView, PermissionGroup.People, false),
        new(Permissions.UsersAvatarRemove, PermissionGroup.People, false),

        new(Permissions.TripsView, PermissionGroup.Trips, false),
        new(Permissions.TripsCancel, PermissionGroup.Trips, false),
        new(Permissions.DestinationsManage, PermissionGroup.Trips, false),

        new(Permissions.BookingsView, PermissionGroup.Money, false),
        new(Permissions.PaymentsView, PermissionGroup.Money, false),
        new(Permissions.PaymentsRefundRetry, PermissionGroup.Money, false),
        new(Permissions.PayoutsView, PermissionGroup.Money, false),
        new(Permissions.PayoutsApprove, PermissionGroup.Money, false),

        new(Permissions.SafetySosView, PermissionGroup.Safety, false),
        new(Permissions.SafetySosManage, PermissionGroup.Safety, false),
        new(Permissions.SafetyCheckInsView, PermissionGroup.Safety, false),
        new(Permissions.SafetyDestinationsStatus, PermissionGroup.Safety, false),
        new(Permissions.SafetyPointsManage, PermissionGroup.Safety, false),

        new(Permissions.ModerationReportsView, PermissionGroup.Moderation, false),
        new(Permissions.ModerationReportsResolve, PermissionGroup.Moderation, false),
        new(Permissions.ModerationContentManage, PermissionGroup.Moderation, false),
        new(Permissions.ModerationDisputes, PermissionGroup.Moderation, false),

        new(Permissions.StaffView, PermissionGroup.Staff, false),
        new(Permissions.StaffAssign, PermissionGroup.Staff, true),
        new(Permissions.StaffRolesManage, PermissionGroup.Staff, true),

        new(Permissions.SettingsBranding, PermissionGroup.Site, false),
        new(Permissions.SettingsTheme, PermissionGroup.Site, false),

        new(Permissions.AuditView, PermissionGroup.System, false),
    ];

    private static readonly Dictionary<string, PermissionDefinition> ByKey =
        All.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    /// <summary>Whether the code knows this key. Writes refuse anything else.</summary>
    public static bool Exists(string key) => ByKey.ContainsKey(key);

    public static PermissionDefinition? Find(string key) =>
        ByKey.TryGetValue(key, out var definition) ? definition : null;

    /// <summary>Whether using this permission needs the password re-entered first.</summary>
    public static bool RequiresStepUp(string key) => Find(key)?.RequiresStepUp == true;

    /// <summary>
    /// The keys from <paramref name="keys"/> that exist, de-duplicated. Used on both sides: to
    /// drop keys the database holds but this version does not know, and to refuse unknown keys
    /// on a write (compare the count with the input).
    /// </summary>
    public static IReadOnlySet<string> Known(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.Where(Exists).ToHashSet(StringComparer.Ordinal);
    }
}
