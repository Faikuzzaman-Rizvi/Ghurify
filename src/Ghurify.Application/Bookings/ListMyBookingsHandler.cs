namespace Ghurify.Application.Bookings;

/// <summary>The signed-in traveller's own requests and bookings. Filtered by user id in SQL.</summary>
public sealed class ListMyBookingsHandler(IJoinRequestRepository requests)
{
    public Task<IReadOnlyList<MyTripBooking>> HandleAsync(long userId, CancellationToken cancellationToken) =>
        requests.QueryMineAsync(userId, cancellationToken);
}
