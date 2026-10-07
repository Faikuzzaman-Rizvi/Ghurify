namespace Ghurify.Domain.Identity;

/// <summary>
/// What a person may do on the platform. Stored as TINYINT in [Main].[UserRole].
/// Every account is a Traveler; that role is implicit and never stored.
/// </summary>
public enum Role : byte
{
    Traveler = 1,
    Host = 2,
    Creator = 3,
    Celebrity = 4,
    Guide = 5,
    Operator = 6,
    Partner = 7,
    Moderator = 8,
    SafetyDesk = 9,
    Admin = 10,
}
