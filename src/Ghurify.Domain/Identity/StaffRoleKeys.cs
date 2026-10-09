namespace Ghurify.Domain.Identity;

/// <summary>
/// The staff roles the platform ships with. They are ordinary rows in
/// <c>[Main].[StaffRole]</c> (seeded by the post-deployment script, marked
/// <c>IsSystem</c> so nobody can delete them), and their permissions can be edited from the
/// portal like any other role's. These constants exist only where code has to name one: the
/// console command that makes the first super admin, and the tests.
/// </summary>
public static class StaffRoleKeys
{
    /// <summary>
    /// Complete control, including the permissions a later release will add. Its permission list
    /// is empty on purpose: <see cref="UserAccess.Can"/> answers yes to everything for it, so
    /// there is no list to keep up to date and no way to leave it short of one.
    /// </summary>
    public const string SuperAdmin = "super-admin";

    /// <summary>Runs the platform day to day: people, trips, money, safety, moderation.</summary>
    public const string Admin = "admin";

    /// <summary>Reports, disputes and user content.</summary>
    public const string Moderator = "moderator";

    /// <summary>SOS, check-ins, destination closures and emergency points.</summary>
    public const string SafetyDesk = "safety-desk";
}
