using System.ComponentModel.DataAnnotations;

namespace Ghurify.Application.Bookings;

/// <summary>Booking policy. Defaults are the product rules; config may tune them per environment.</summary>
public sealed class BookingOptions
{
    public const string SectionName = "Bookings";

    /// <summary>How long an approved traveller has to pay before the seat is released.</summary>
    [Range(5, 1440)]
    public int HoldMinutes { get; set; } = 30;
}
