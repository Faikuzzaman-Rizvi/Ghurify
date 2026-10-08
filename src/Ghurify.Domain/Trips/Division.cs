namespace Ghurify.Domain.Trips;

/// <summary>
/// The eight administrative divisions of Bangladesh. The names match [Main].[Destination].[Division];
/// the numbers are what [Main].[Visit].[Division] stores for a place someone added themselves.
/// </summary>
public enum Division : byte
{
    Barishal = 1,
    Chattogram = 2,
    Dhaka = 3,
    Khulna = 4,
    Mymensingh = 5,
    Rajshahi = 6,
    Rangpur = 7,
    Sylhet = 8,
}
