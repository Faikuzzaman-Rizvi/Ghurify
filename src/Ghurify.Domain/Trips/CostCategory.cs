namespace Ghurify.Domain.Trips;

/// <summary>A line of a trip's per-person cost breakdown.</summary>
public enum CostCategory : byte
{
    Transport = 1,
    Stay = 2,
    Food = 3,
    Fees = 4,
    Guide = 5,
    Buffer = 6,
}