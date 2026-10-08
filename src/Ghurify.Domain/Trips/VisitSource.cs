namespace Ghurify.Domain.Trips;

/// <summary>How a place got onto someone's travel map. Stored as TINYINT in [Main].[Visit].</summary>
public enum VisitSource : byte
{
    /// <summary>A Ghurify trip they hosted or paid for, recorded when it was completed.</summary>
    Trip = 1,

    /// <summary>They added it themselves.</summary>
    Added = 2,
}
