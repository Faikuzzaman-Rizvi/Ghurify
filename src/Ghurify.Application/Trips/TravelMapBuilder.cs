using System.Globalization;

namespace Ghurify.Application.Trips;

/// <summary>
/// Turns visits, one by one, into a map: one pin per place, the places most recently visited first,
/// each with its visits (newest first) and photos, and the summary at the top.
///
/// A Ghurify destination is one place however often it was visited. A place someone added
/// themselves is matched by its name and position (to about 100 m), so going back to the same
/// village twice gives one pin with two visits rather than two pins on top of each other.
/// </summary>
public static class TravelMapBuilder
{
    /// <param name="link">Turns a photo's blob name into a link; null leaves the photo out.</param>
    public static TravelMap Build(TravelMapData data, Func<string, string?> link)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(link);

        PlacePhoto? Signed(PlacePhotoRecord photo) =>
            link(photo.Blob) is { } url ? new PlacePhoto(photo.MediaId, photo.PostId, photo.VisitId, photo.Kind, url) : null;

        var storyPhotos = data.Photos
            .Where(photo => photo.DestinationSlug is not null)
            .GroupBy(photo => photo.DestinationSlug!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PlacePhoto>)[.. group.Select(Signed).OfType<PlacePhoto>()],
                StringComparer.Ordinal);

        var visitPhotos = data.Photos
            .Where(photo => photo.VisitId is not null)
            .GroupBy(photo => photo.VisitId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(Signed).OfType<PlacePhoto>().ToList());

        // The visits arrive newest first, and grouping keeps that order inside each group.
        var places = data.Visits
            .GroupBy(PlaceOf, StringComparer.Ordinal)
            .Select(group =>
            {
                var latest = group.First();
                return new VisitedPlace(
                    Key(group.Key, group),
                    latest.DestinationSlug,
                    latest.DestinationName ?? latest.PlaceName ?? string.Empty,
                    latest.DestinationNameBn,
                    latest.Division,
                    latest.Kind,
                    latest.Latitude,
                    latest.Longitude,
                    latest.VisitedOn,
                    [.. group.Select(visit => new PlaceVisit(visit.Id, visit.Source, visit.VisitedOn, visit.Note, visit.Trip, visit.PhotosProcessing))],
                    [
                        .. group.SelectMany(visit => visitPhotos.TryGetValue(visit.Id, out var own) ? own : []),
                        .. latest.DestinationSlug is { } slug && storyPhotos.TryGetValue(slug, out var found) ? found : [],
                    ]);
            })
            .OrderByDescending(place => place.LastVisitedOn)
            .ToList();

        var trips = data.Visits
            .Select(visit => visit.Trip)
            .OfType<VisitTrip>()
            .DistinctBy(trip => trip.Id)
            .ToList();

        var summary = new TravelSummary(
            places.Count,
            places.Select(place => place.Division).OfType<Domain.Trips.Division>().Distinct().Count(),
            trips.Count,
            trips.Sum(trip => trip.EndDate.DayNumber - trip.StartDate.DayNumber + 1),
            data.Visits.Count == 0 ? null : data.Visits.Min(visit => visit.VisitedOn),
            data.Visits.Count == 0 ? null : data.Visits.Max(visit => visit.VisitedOn));

        return new TravelMap(data.UserId, data.DisplayName, data.Shared, summary, places, data.Upcoming);
    }

    /// <summary>What makes two visits the same place: the destination, or the name and position.</summary>
    private static string PlaceOf(VisitRecord visit) =>
        visit.DestinationSlug is { } slug
            ? "d:" + slug
            : string.Create(
                CultureInfo.InvariantCulture,
                $"p:{visit.PlaceName?.Trim().ToUpperInvariant()}|{Math.Round(visit.Latitude ?? 0, 3)}|{Math.Round(visit.Longitude ?? 0, 3)}");

    /// <summary>
    /// The pin's key for the page: the destination's slug, or the place's oldest visit id (stable
    /// while that visit stays; never the name, which is the owner's own text).
    /// </summary>
    private static string Key(string place, IEnumerable<VisitRecord> visits) =>
        place.StartsWith("d:", StringComparison.Ordinal)
            ? place
            : "p:" + visits.Min(visit => visit.Id).ToString(CultureInfo.InvariantCulture);
}
