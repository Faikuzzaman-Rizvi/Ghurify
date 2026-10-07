using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>A profile edit as the caller sends it. Phone numbers are typed, not yet normalised.</summary>
public sealed record UpdateProfileCommand(
    string DisplayName,
    Gender? Gender,
    string? Phone,
    string? Bio,
    string? HomeDistrict,
    string? EmergencyContactName,
    string? EmergencyContactPhone);
