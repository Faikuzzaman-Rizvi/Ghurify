namespace Ghurify.Domain.Identity;

/// <summary>
/// An account. Created the first time an email address completes an OTP sign-in.
///
/// Email is the sign-in identity. Phone is optional: it is collected later on the profile,
/// because SOS alerts, chat number-masking and payouts all need a real number.
/// </summary>
public sealed class User(
    long id,
    EmailAddress email,
    PhoneNumber? phone,
    string? displayName,
    Gender? gender,
    UserStatus status,
    DateTimeOffset created)
{
    public long Id { get; } = id;

    public EmailAddress Email { get; } = email;

    public PhoneNumber? Phone { get; } = phone;

    public string? DisplayName { get; } = displayName;

    public Gender? Gender { get; } = gender;

    public UserStatus Status { get; } = status;

    public DateTimeOffset Created { get; } = created;

    /// <summary>
    /// Whether this account may sign in. A suspended or closed account must not be issued
    /// tokens, however correct its OTP was.
    /// </summary>
    public bool CanSignIn => Status == UserStatus.Active;

    /// <summary>
    /// Whether the account can be reached for safety alerts. Trips and SOS need a number,
    /// so later sprints gate on this rather than assuming one exists.
    /// </summary>
    public bool HasVerifiedContactNumber => Phone is not null;
}
