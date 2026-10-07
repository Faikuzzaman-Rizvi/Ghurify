namespace Ghurify.Application.Payments;

/// <summary>The signed-in host's own payouts. Filtered by host id in SQL.</summary>
public sealed class ListMyPayoutsHandler(IPayoutRepository payouts)
{
    public Task<IReadOnlyList<PayoutView>> HandleAsync(long hostId, CancellationToken cancellationToken) =>
        payouts.QueryAsync(hostId, status: null, cancellationToken);
}
