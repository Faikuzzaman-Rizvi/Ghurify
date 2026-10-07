using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Trips;

/// <summary>
/// Saves a new trip as a draft. Any host may draft; publishing is where verification is required,
/// so a new host can prepare a trip while their identity check is still in progress.
/// </summary>
public sealed class CreateTripHandler(
    ITripRepository trips,
    IDestinationRepository destinations,
    AccessService access,
    TripViewer viewer,
    ILogger<CreateTripHandler> logger)
{
    public async Task<Result<TripCreated>> HandleAsync(
        long hostId,
        SaveTripCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var host = await access.GetAsync(hostId, cancellationToken);
        if (!host.IsActive || !host.Has(Role.Host))
        {
            return AppError.Forbidden("Turn on hosting in your account before creating a trip.");
        }

        var destination = await destinations.FindBySlugAsync(command.DestinationSlug, cancellationToken);
        if (destination is null)
        {
            return TripWriting.UnknownDestination();
        }

        if (TripWriting.ToPlan(command).CheckForSave(viewer.TodayInDhaka, host.Gender) is { } violation)
        {
            return TripWriting.ToError(violation);
        }

        var id = await trips.AddAsync(hostId, TripWriting.ToWrite(command, destination.Id), cancellationToken);

        logger.LogInformation("Host {HostId} drafted trip {TripId} to {Destination}.", hostId, id, destination.Slug);
        return new TripCreated(id);
    }
}
