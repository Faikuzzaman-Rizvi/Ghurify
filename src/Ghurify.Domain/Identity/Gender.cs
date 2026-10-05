namespace Ghurify.Domain.Identity;

/// <summary>
/// Stored as TINYINT in [Main].[User], nullable: nobody is asked for it at sign-up.
/// It matters later, because women-only trips are restricted by it.
/// </summary>
public enum Gender : byte
{
    Unspecified = 0,
    Female = 1,
    Male = 2,
    Other = 3,
}
