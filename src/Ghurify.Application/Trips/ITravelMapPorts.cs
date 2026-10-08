using Ghurify.Application.Social;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Trips;

/// <summary>Travel maps: the places in Bangladesh someone has been, and the trips still to come.</summary>
public interface ITravelMapRepository
{
    /// <summary>
    /// One person's map. <paramref name="includePrivate"/> is the owner looking: everything. Anyone
    /// else gets nothing unless it is shared, and then only visits to Ghurify destinations, with no
    /// notes and no trips to come. Null when there is no such active person.
    /// </summary>
    Task<TravelMapData?> GetAsync(long userId, bool includePrivate, DateOnly today, CancellationToken cancellationToken);

    Task<VisitAdded> AddVisitAsync(NewVisit visit, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the note, and the date of a visit they added (a trip's date is the trip's). Only the
    /// owner's own visit is touched; false when there is none.
    /// </summary>
    Task<bool> UpdateVisitAsync(long visitId, long userId, DateOnly? visitedOn, string? note, CancellationToken cancellationToken);

    /// <summary>Takes a visit off the owner's map. False when there is no such visit of theirs.</summary>
    Task<bool> RemoveVisitAsync(long visitId, long userId, CancellationToken cancellationToken);

    Task SetSharingAsync(long userId, bool share, CancellationToken cancellationToken);

    /// <summary>
    /// Puts the owner's own uploaded photos on one of their visits: images only, not on a story or
    /// another visit, at most <paramref name="maxPhotos"/> on the visit. All or nothing.
    /// </summary>
    Task<AddPhotosOutcome> AddPhotosAsync(long userId, long visitId, IReadOnlyList<long> mediaIds, int maxPhotos, CancellationToken cancellationToken);

    /// <summary>Takes one photo off the owner's visit. False when there is no such photo of theirs there.</summary>
    Task<bool> RemovePhotoAsync(long userId, long visitId, long mediaId, CancellationToken cancellationToken);
}

public enum AddPhotosOutcome
{
    Added = 0,
    VisitNotFound = 1,
    Unusable = 2,
    TooMany = 3,
}

/// <summary>
/// What the repository reads: the visits one by one, photos (by place from stories, by visit from
/// the owner's own uploads), and trips to come.
/// </summary>
public sealed record TravelMapData(
    long UserId,
    string? DisplayName,
    bool Shared,
    IReadOnlyList<VisitRecord> Visits,
    IReadOnlyList<PlacePhotoRecord> Photos,
    IReadOnlyList<UpcomingTrip> Upcoming);

/// <summary>One visit, with its place (a destination, or a named point) and the trip it came from.</summary>
public sealed record VisitRecord(
    long Id,
    VisitSource Source,
    DateOnly VisitedOn,
    string? Note,
    string? DestinationSlug,
    string? DestinationName,
    string? DestinationNameBn,
    DestinationKind? Kind,
    string? PlaceName,
    Division? Division,
    double? Latitude,
    double? Longitude,
    VisitTrip? Trip,
    int PhotosProcessing);

/// <summary>A story photo about a destination (DestinationSlug, PostId), or one put on a visit (VisitId).</summary>
public sealed record PlacePhotoRecord(string? DestinationSlug, long? VisitId, long MediaId, long? PostId, MediaKind Kind, string Blob);

public sealed record NewVisit(
    long UserId,
    string? DestinationSlug,
    string? PlaceName,
    Division? Division,
    double? Latitude,
    double? Longitude,
    DateOnly VisitedOn,
    string? Note,
    int MaxAdded,
    IReadOnlyList<long> MediaIds,
    int MaxPhotos);

public sealed record VisitAdded(AddVisitOutcome Outcome, long? Id);

public enum AddVisitOutcome
{
    Added = 0,
    UnknownDestination = 1,
    AlreadyOnMap = 2,
    TooMany = 3,
    PhotoUnusable = 4,
    TooManyPhotos = 5,
}

// --- What the API returns ---

/// <summary>A travel map: the places, grouped one pin per place, a summary, and trips to come.</summary>
public sealed record TravelMap(
    long UserId,
    string? DisplayName,
    bool Shared,
    TravelSummary Summary,
    IReadOnlyList<VisitedPlace> Places,
    IReadOnlyList<UpcomingTrip> Upcoming);

/// <summary>
/// Places pinned, divisions reached (of eight), Ghurify trips taken and the days they lasted, and
/// the first and latest visit.
/// </summary>
public sealed record TravelSummary(int Places, int Divisions, int Trips, int TripDays, DateOnly? FirstVisit, DateOnly? LastVisit);

/// <summary>
/// One pin: a Ghurify destination, or a place someone named themselves, with every visit there
/// (newest first) and its photos: the ones they put on their visits first, then their stories'.
/// </summary>
public sealed record VisitedPlace(
    string Key,
    string? DestinationSlug,
    string Name,
    string? NameBn,
    Division? Division,
    DestinationKind? Kind,
    double? Latitude,
    double? Longitude,
    DateOnly LastVisitedOn,
    IReadOnlyList<PlaceVisit> Visits,
    IReadOnlyList<PlacePhoto> Photos);

/// <summary>One visit. PhotosProcessing: photos of theirs still being checked (the owner's view only).</summary>
public sealed record PlaceVisit(long Id, VisitSource Source, DateOnly VisitedOn, string? Note, VisitTrip? Trip, int PhotosProcessing);

public sealed record VisitTrip(long Id, string Title, DateOnly StartDate, DateOnly EndDate, string? HostName, bool AsHost);

/// <summary>A photo at a place: from a story (PostId), or put on one of the visits (VisitId).</summary>
public sealed record PlacePhoto(long MediaId, long? PostId, long? VisitId, MediaKind Kind, string Url);

/// <summary>A confirmed trip still to come: shown to its traveller as a pin for where they are going.</summary>
public sealed record UpcomingTrip(
    long TripId,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    bool AsHost,
    string DestinationSlug,
    string DestinationName,
    string DestinationNameBn,
    double? Latitude,
    double? Longitude);
