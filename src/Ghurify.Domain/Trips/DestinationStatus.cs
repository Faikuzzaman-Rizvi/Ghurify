namespace Ghurify.Domain.Trips;

/// <summary>
/// The safety signal for a destination. Caution puts a warning on every trip there; Closed
/// stops new trips being published to it.
/// </summary>
public enum DestinationStatus : byte
{
    Open = 1,
    Caution = 2,
    Closed = 3,
}