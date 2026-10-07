namespace Ghurify.Domain.Identity;

/// <summary>What an emailed code is for. Stored as TINYINT in [Main].[OtpCode].</summary>
public enum OtpPurpose : byte
{
    /// <summary>Confirms the email address of a new account. Sent once, at sign-up.</summary>
    SignUp = 1,

    /// <summary>Lets the owner of the address choose a new password.</summary>
    PasswordReset = 2,
}
