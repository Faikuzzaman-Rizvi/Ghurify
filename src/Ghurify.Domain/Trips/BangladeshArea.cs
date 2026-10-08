namespace Ghurify.Domain.Trips;

/// <summary>
/// Where a pin may go: the box around Bangladesh, from Saint Martin's Island (about 20.6° N) to
/// Tetulia (about 26.6° N) and from the western border (about 88.0° E) to the Chittagong Hill Tracts
/// (about 92.7° E). A box, not the border: it keeps pins out of the rest of the world, but places
/// just across the line (Kolkata, Agartala, Shillong) fit in it too. For someone's own travel map
/// that is harmless, and it never turns away a real place near the border.
/// </summary>
public static class BangladeshArea
{
    public const double South = 20.55;
    public const double North = 26.65;
    public const double West = 87.95;
    public const double East = 92.75;

    public static bool Contains(double latitude, double longitude) =>
        latitude is >= South and <= North && longitude is >= West and <= East;
}
