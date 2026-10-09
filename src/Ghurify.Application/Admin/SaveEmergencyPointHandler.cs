using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// The safety desk keeps the police stations and hospitals shown with an SOS right: adds them,
/// corrects names, phones and positions, and marks them checked (audited). The seeded ones are
/// approximate until someone does.
/// </summary>
public sealed class SaveEmergencyPointHandler(IAdminRepository admin, AccessService access, IAuditLog audit)
{
    public async Task<Result<EmergencyPointSaved>> HandleAsync(long actorId, EmergencyPointEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.SafetyPointsManage))
        {
            return AppError.Forbidden();
        }

        if (edit.Kind is < 1 or > 3)
        {
            return AppError.Validation("point_kind", "Choose police, hospital or tourist police.");
        }

        if (string.IsNullOrWhiteSpace(edit.Name) || string.IsNullOrWhiteSpace(edit.NameBn) || edit.Name.Trim().Length > 150 || edit.NameBn.Trim().Length > 150)
        {
            return AppError.Validation("point_name", "Give the name in both languages.");
        }

        var phone = string.IsNullOrWhiteSpace(edit.Phone) ? null : edit.Phone.Trim();
        if (phone is not null && (phone.Length is < 3 or > 20 || phone.Any(character => !char.IsAsciiDigit(character) && character is not ('+' or '-' or ' '))))
        {
            return AppError.Validation("point_phone", "Write the phone number with digits only, for example 01320-000000.");
        }

        if (edit.Latitude is < 20m or > 27m || edit.Longitude is < 88m or > 93m)
        {
            return AppError.Validation("point_position", "The position must be inside Bangladesh.");
        }

        var id = await admin.SetEmergencyPointAsync(
            edit with { Name = edit.Name.Trim(), NameBn = edit.NameBn.Trim(), Phone = phone }, actorId, cancellationToken);
        if (id is null)
        {
            return AppError.NotFound("point_not_found", "There is no such emergency point.");
        }

        await audit.WriteAsync(new AuditRecord(actorId, edit.Id is null ? "emergency_point.added" : "emergency_point.edited", "EmergencyPoint", id.Value, null), cancellationToken);
        return new EmergencyPointSaved(id.Value);
    }
}

public sealed record EmergencyPointSaved(long Id);
