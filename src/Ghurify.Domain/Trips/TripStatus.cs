namespace Ghurify.Domain.Trips;

/// <summary>Where a trip is in its life. Only Published and Full trips are public.</summary>
public enum TripStatus : byte
{
    Draft = 1,
    Published = 2,
    Full = 3,
    Cancelled = 4,
    Completed = 5,
}