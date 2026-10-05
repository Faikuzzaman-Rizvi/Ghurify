namespace Ghurify.Application.Trips;

/// <summary>Finds live trips for the explore page.</summary>
public sealed class SearchTripsHandler(ITripRepository trips, TripViewer viewer)
{
    public async Task<TripPage> HandleAsync(
        SearchTripsQuery query,
        long? viewerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Past trips are never searchable, whatever "from" the caller sends.
        var today = viewer.TodayInDhaka;
        var from = query.From is { } requested && requested > today ? requested : today;

        var criteria = new TripSearchCriteria(
            FromDate: from,
            ToDate: query.To,
            DestinationSlug: string.IsNullOrWhiteSpace(query.Destination) ? null : query.Destination.Trim(),
            MaxPrice: query.MaxPrice,
            GroupType: query.GroupType,
            MinSeats: query.MinSeats,
            IncludeWomenOnly: await viewer.IncludesWomenOnlyAsync(viewerId, cancellationToken),
            Sort: query.Sort,
            Offset: (query.Page - 1) * query.PageSize,
            PageSize: query.PageSize);

        var page = await trips.SearchAsync(criteria, cancellationToken);

        return page with { Page = query.Page, PageSize = query.PageSize };
    }
}
