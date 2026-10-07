using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>Saves the signed-in user's own profile. A user can only ever write their own row.</summary>
public sealed class UpdateProfileHandler(
    IProfileRepository profiles,
    AccessService access,
    ILogger<UpdateProfileHandler> logger)
{
    public async Task<Result<ProfileDetails>> HandleAsync(
        long userId,
        UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Already validated at the boundary: a blank number stays null, anything else parses.
        var phone = PhoneNumber.TryParse(command.Phone, out var parsedPhone) ? parsedPhone : null;
        var emergencyPhone = PhoneNumber.TryParse(command.EmergencyContactPhone, out var parsedEmergency)
            ? parsedEmergency
            : null;

        if (phone is not null && phone == emergencyPhone)
        {
            return AppError.Validation(
                "emergency_contact_is_self",
                "Your emergency contact must be someone else's number.");
        }

        var current = await profiles.GetAsync(userId, cancellationToken);
        if (current is null)
        {
            return AppError.NotFound("profile_not_found", "There is no profile for this account.");
        }

        // Gender gates women-only trips. Once an identity check has passed it is fixed to what
        // was verified, and only support can change it, so nobody can verify as one gender and
        // then switch to join trips meant for another.
        if (current.VerifiedLevel is not null && current.Gender is not null && command.Gender != current.Gender)
        {
            return AppError.Rule(
                "gender_locked",
                "Your gender is fixed after identity verification. Contact support to correct it.");
        }

        var update = new ProfileUpdate(
            command.DisplayName.Trim(),
            command.Gender,
            phone,
            Clean(command.Bio),
            Clean(command.HomeDistrict),
            Clean(command.EmergencyContactName),
            emergencyPhone);

        if (!await profiles.SaveAsync(userId, update, cancellationToken))
        {
            return AppError.Conflict("phone_in_use", "This phone number is already used by another account.");
        }

        access.Forget(userId);
        logger.LogInformation("User {UserId} updated their profile.", userId);

        return (await profiles.GetAsync(userId, cancellationToken))!;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
