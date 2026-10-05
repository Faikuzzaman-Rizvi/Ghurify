namespace Ghurify.Domain.Identity;

/// <summary>Lifecycle of an account. Stored as TINYINT in [Main].[User].</summary>
public enum UserStatus : byte
{
    /// <summary>Signed up and able to use the platform.</summary>
    Active = 1,

    /// <summary>Temporarily blocked, for example while a report is investigated.</summary>
    Suspended = 2,

    /// <summary>Closed by the user. Kept so trips and payments stay attributable.</summary>
    Deactivated = 3,
}
