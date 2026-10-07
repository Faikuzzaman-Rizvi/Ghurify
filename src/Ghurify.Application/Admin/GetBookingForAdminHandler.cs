using System.Globalization;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// A booking with its payments, refunds and escrow balance, by booking number or by the payment
/// reference a traveller quotes when they write in.
/// </summary>
public sealed class GetBookingForAdminHandler(IAdminRepository admin, AccessService access)
{
    public async Task<Result<AdminBookingDetail>> HandleAsync(long actorId, string? lookup, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        var term = lookup?.Trim() ?? string.Empty;
        if (term.Length is 0 or > 64)
        {
            return AppError.Validation("lookup_required", "Enter a booking number or a payment reference.");
        }

        var booking = long.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? await admin.GetBookingAsync(id, null, cancellationToken)
            : await admin.GetBookingAsync(null, term, cancellationToken);

        return booking is null ? AppError.NotFound("booking_not_found", "No booking matches that number or reference.") : booking;
    }
}
