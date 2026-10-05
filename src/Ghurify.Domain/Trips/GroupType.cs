namespace Ghurify.Domain.Trips;

/// <summary>Who a trip is for. Stored as TINYINT in [Main].[Trip].</summary>
public enum GroupType : byte
{
    Open = 1,
    WomenOnly = 2,
    Students = 3,
    Families = 4,
}