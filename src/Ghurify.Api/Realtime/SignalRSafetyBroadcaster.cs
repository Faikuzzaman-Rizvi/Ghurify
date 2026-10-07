using Ghurify.Application.Safety;
using Microsoft.AspNetCore.SignalR;

namespace Ghurify.Api.Realtime;

/// <summary>Pushes SOS alerts and missed check-ins to everyone on the safety desk.</summary>
public sealed class SignalRSafetyBroadcaster(IHubContext<SafetyHub> hub) : ISafetyBroadcaster
{
    public Task SosAsync(SosBoardItem sos, CancellationToken cancellationToken) =>
        hub.Clients.Group(SafetyHub.DeskGroup).SendAsync(SafetyHub.SosEvent, sos, cancellationToken);

    public Task CheckInMissedAsync(MissedCheckIn checkIn, CancellationToken cancellationToken) =>
        hub.Clients.Group(SafetyHub.DeskGroup).SendAsync(SafetyHub.CheckInMissedEvent, checkIn, cancellationToken);
}
