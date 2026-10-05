namespace Ghurify.Domain.Trips;

/// <summary>What a destination is like. Stored as TINYINT in [Main].[Destination].</summary>
public enum DestinationKind : byte
{
    Hills = 1,
    Beach = 2,
    Island = 3,
    Forest = 4,
    Wetland = 5,
    TeaGarden = 6,
    Lake = 7,
    River = 8,
}