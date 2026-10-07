using Ghurify.Domain.Trips;

namespace Ghurify.Application.Trips;

/// <summary>A trip as the host's wizard sends it, for both create and update.</summary>
public sealed record SaveTripCommand(
    string DestinationSlug,
    string Title,
    string Summary,
    DateOnly StartDate,
    DateOnly EndDate,
    string MeetingPoint,
    int Seats,
    decimal PricePerPerson,
    GroupType GroupType,
    IReadOnlyList<SaveTripCostLine> CostItems,
    IReadOnlyList<SaveTripDay> Itinerary);

public sealed record SaveTripCostLine(CostCategory Category, string? Description, decimal Amount);

public sealed record SaveTripDay(int DayNo, string Title, string Details, Difficulty Difficulty);

/// <summary>A trip as the repository writes it: the destination resolved, the text trimmed.</summary>
public sealed record TripWrite(
    long DestinationId,
    string Title,
    string Summary,
    DateOnly StartDate,
    DateOnly EndDate,
    string MeetingPoint,
    int Seats,
    decimal PricePerPerson,
    GroupType GroupType,
    IReadOnlyList<SaveTripCostLine> CostItems,
    IReadOnlyList<SaveTripDay> Itinerary);

/// <summary>What happened to a write to one of a host's trips.</summary>
public enum TripWriteOutcome
{
    Saved = 0,
    NotFound = 1,
    NotEditable = 2,
    SeatsBelowTaken = 3,
    TermsLocked = 4,
    DestinationClosed = 5,
    NotDraft = 6,
}

/// <summary>The id of a created trip.</summary>
public sealed record TripCreated(long Id);

/// <summary>A destination as trip writes need it: its id and safety status.</summary>
public sealed record DestinationRef(long Id, string Slug, DestinationStatus Status);

/// <summary>One of a host's own trips, any status, for the host dashboard.</summary>
public sealed record HostTripSummary(
    long Id,
    string Title,
    TripDestination Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int Seats,
    int SeatsTaken,
    decimal PricePerPerson,
    GroupType GroupType,
    TripStatus Status,
    int PendingRequests);
