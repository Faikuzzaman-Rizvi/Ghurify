using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Safety;

/// <summary>
/// The safety desk acknowledges or resolves an SOS (audited), or the person who raised it says they
/// are safe now.
/// </summary>
public sealed class SetSosStatusHandler(ISafetyRepository safety, AccessService access, IAuditLog audit)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long sosId, SosStatus status, CancellationToken cancellationToken)
    {
        if (status == SosStatus.Open)
        {
            return AppError.Validation("sos_status", "An SOS can be acknowledged or resolved.");
        }

        var desk = (await access.GetAsync(actorId, cancellationToken)).Can(Permissions.SafetySosManage);

        // Not the desk: only the owner, and only to say they are safe.
        if (!desk && status != SosStatus.Resolved)
        {
            return AppError.Forbidden();
        }

        var changed = await safety.SetSosStatusAsync(sosId, status, actorId, desk ? null : actorId, cancellationToken);
        if (!changed)
        {
            return AppError.NotFound("sos_not_found", "There is no such open SOS.");
        }

        if (desk)
        {
            await audit.WriteAsync(
                new AuditRecord(
                    actorId,
                    status == SosStatus.Resolved ? "sos.resolve" : "sos.acknowledge",
                    "SosEvent",
                    sosId,
                    Changes: new AuditChanges().Set("status", SosStatus.Open, status).ToJson()),
                cancellationToken);
        }

        return Done.Value;
    }
}
