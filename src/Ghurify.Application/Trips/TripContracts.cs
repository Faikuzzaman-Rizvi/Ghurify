using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Trips;

/// <summary>How search results are ordered.</summary>
public enum TripSort : byte
{
    Soonest = 0,
    PriceLowToHigh = 1,
    PriceHighToLow = 2,
}

/// <summary>A trip search as the caller asks for it. Every filter is optional.</summary>
public sealed record SearchTripsQuery(
    string? Destination = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? MaxPrice = null,
    GroupType? GroupType = null,
    int? MinSeats = null,
    TripSort Sort = TripSort.Soonest,
    int Page = 1,
    int PageSize = SearchTripsQuery.DefaultPageSize,
    bool VerifiedHostsOnly = false)
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 48;
}

/// <summary>
/// A search as the repository runs it: the date floor resolved, paging turned into an offset,
/// and the women-only decision already made for this viewer.
/// </summary>
public sealed record TripSearchCriteria(
    DateOnly FromDate,
    DateOnly? ToDate,
    string? DestinationSlug,
    decimal? MaxPrice,
    GroupType? GroupType,
    int? MinSeats,
    bool IncludeWomenOnly,
    TripSort Sort,
    int Offset,
    int PageSize,
    bool VerifiedHostsOnly = false);

/// <summary>One trip card in search results.</summary>
public sealed record TripSummary(
    long Id,
    string Title,
    TripDestination Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int Seats,
    int SeatsLeft,
    decimal PricePerPerson,
    GroupType GroupType,
    TripStatus Status,
    string? HostName,
    VerificationLevel? HostVerifiedLevel = null);

/// <summary>Where a trip goes, as much as a card or page needs.</summary>
public sealed record TripDestination(
    string Slug,
    string Name,
    string NameBn,
    DestinationKind Kind,
    DestinationStatus Status,
    string? StatusNote = null,
    string? StatusNoteBn = null);

/// <summary>A page of results and the total across all pages.</summary>
public sealed record TripPage(IReadOnlyList<TripSummary> Items, int TotalCount, int Page, int PageSize);

/// <summary>A trip's public page.</summary>
public sealed record TripDetail(
    long Id,
    string Title,
    string Summary,
    TripDestination Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    string MeetingPoint,
    int Seats,
    int SeatsLeft,
    decimal PricePerPerson,
    GroupType GroupType,
    TripStatus Status,
    TripHost Host,
    IReadOnlyList<TripCostLine> CostItems,
    IReadOnlyList<TripItineraryDay> Itinerary,
    TripGroupMix? GroupMix = null);

/// <summary>Who holds a seat, by gender: counts only, never names.</summary>
public sealed record TripGroupMix(int Women, int Men, int Others);

/// <summary>The host as a traveller sees them. Never the host's contact details.</summary>
public sealed record TripHost(long Id, string? DisplayName, DateOnly MemberSince, VerificationLevel? VerifiedLevel = null);

/// <summary>One line of the cost breakdown.</summary>
public sealed record TripCostLine(CostCategory Category, string? Description, decimal Amount);

/// <summary>One day of the plan.</summary>
public sealed record TripItineraryDay(int DayNo, string Title, string Details, Difficulty Difficulty);

/// <summary>A destination card: what it is, its safety status, and what is on offer there.</summary>
public sealed record DestinationSummary(
    string Slug,
    string Name,
    string NameBn,
    string Division,
    string DivisionBn,
    string Summary,
    string SummaryBn,
    DestinationKind Kind,
    DestinationStatus Status,
    string? StatusNote,
    string? StatusNoteBn,
    double? Latitude,
    double? Longitude,
    int UpcomingTrips,
    decimal? FromPrice);
