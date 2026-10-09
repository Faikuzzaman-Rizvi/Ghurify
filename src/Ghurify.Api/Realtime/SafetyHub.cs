using Ghurify.Api.Endpoints;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ghurify.Api.Realtime;

/// <summary>
/// /hubs/safety: the safety desk's live board. SOS alerts, their moving positions and missed
/// check-ins are pushed to the "safety-desk" group, which only staff who may watch the board can
/// join (checked against the database on every join, not against the token).
/// </summary>
[Authorize]
public sealed class SafetyHub(AccessService access) : Hub
{
    public const string Path = "/hubs/safety";
    public const string DeskGroup = "safety-desk";
    public const string SosEvent = "sos";
    public const string CheckInMissedEvent = "checkInMissed";

    public async Task JoinDesk()
    {
        var userId = Context.User?.FindUserId() ?? throw new HubException("signed_out");

        if (!(await access.GetAsync(userId, Context.ConnectionAborted)).Can(Permissions.SafetySosView))
        {
            throw new HubException("forbidden");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, DeskGroup, Context.ConnectionAborted);
    }
}
