namespace Ghurify.Application.Trips;

/// <summary>Destinations for the home page cards, the search filter and a destination's own page.</summary>
public sealed class ListDestinationsHandler(IDestinationRepository destinations, TripViewer viewer)
{
    public async Task<IReadOnlyList<DestinationSummary>> HandleAsync(
        long? viewerId,
        CancellationToken cancellationToken)
    {
        var includeWomenOnly = await viewer.IncludesWomenOnlyAsync(viewerId, cancellationToken);
        return await destinations.QueryAsync(viewer.TodayInDhaka, includeWomenOnly, cancellationToken);
    }

    /// <summary>
    /// One destination by slug, or null. There are a handful of destinations, so this reads the
    /// same list rather than keeping a second query in step with the first.
    /// </summary>
    public async Task<DestinationSummary?> HandleAsync(
        string slug,
        long? viewerId,
        CancellationToken cancellationToken)
    {
        var all = await HandleAsync(viewerId, cancellationToken);
        return all.FirstOrDefault(destination =>
            string.Equals(destination.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}
