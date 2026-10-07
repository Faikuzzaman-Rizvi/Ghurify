namespace Ghurify.Application.Payments;

/// <summary>The signed-in traveller's refunds and where each stands. Filtered by user id in SQL.</summary>
public sealed class ListMyRefundsHandler(IRefundRepository refunds)
{
    public Task<IReadOnlyList<RefundView>> HandleAsync(long userId, CancellationToken cancellationToken) =>
        refunds.QueryForUserAsync(userId, cancellationToken);
}
