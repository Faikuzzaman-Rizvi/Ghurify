using Ghurify.Application.Notifications;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghurify.UnitTests.Bookings;

/// <summary>Who may ask to join, and notifications that must never go out twice.</summary>
public sealed class BookingRulesTests
{
    [Fact]
    public void JoinEligibility_AnUnverifiedTraveller_IsRefused()
    {
        Assert.Equal("traveler_not_verified", JoinEligibility.Check(Traveller(verified: null), GroupType.Open));
    }

    [Fact]
    public void JoinEligibility_AManOnAWomenOnlyTrip_IsRefused()
    {
        Assert.Equal("women_only_trip", JoinEligibility.Check(Traveller(Gender.Male), GroupType.WomenOnly));
    }

    [Fact]
    public void JoinEligibility_AVerifiedWomanOnAWomenOnlyTrip_IsAllowed()
    {
        Assert.Null(JoinEligibility.Check(Traveller(Gender.Female), GroupType.WomenOnly));
    }

    [Fact]
    public void JoinEligibility_ASuspendedAccount_IsRefused()
    {
        var suspended = Traveller() with { Status = UserStatus.Suspended };

        Assert.Equal("account_inactive", JoinEligibility.Check(suspended, GroupType.Open));
    }

    [Fact]
    public async Task Notify_TheSameEventTwice_StoresAndPushesOnce()
    {
        var store = new FakeNotificationStore();
        var push = new RecordingNotifier();
        var service = new NotificationService(store, push, NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(5, NotificationKinds.HoldExpired, "booking.hold_expired:9", new { bookingId = 9 }, CancellationToken.None);
        await service.NotifyAsync(5, NotificationKinds.HoldExpired, "booking.hold_expired:9", new { bookingId = 9 }, CancellationToken.None);

        Assert.Single(store.Stored);
        Assert.Single(push.Pushed);
    }

    [Fact]
    public async Task Notify_WhenThePushFails_TheNotificationIsStillStored()
    {
        var store = new FakeNotificationStore();
        var service = new NotificationService(store, new FailingNotifier(), NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(5, NotificationKinds.JoinRequestNew, "join_request.new:1", null, CancellationToken.None);

        Assert.Single(store.Stored);
    }

    private static UserAccess Traveller(Gender gender = Gender.Female, VerificationLevel? verified = VerificationLevel.Nid) =>
        new(1, UserStatus.Active, gender, new HashSet<Role>(), verified);

    private sealed class FakeNotificationStore : INotificationRepository
    {
        public List<(long UserId, string Key)> Stored { get; } = [];

        public Task<NotificationItem?> AddAsync(long userId, string kind, string? data, string dedupeKey, CancellationToken cancellationToken)
        {
            if (Stored.Contains((userId, dedupeKey)))
            {
                return Task.FromResult<NotificationItem?>(null);
            }

            Stored.Add((userId, dedupeKey));
            return Task.FromResult<NotificationItem?>(new NotificationItem(Stored.Count, kind, data, DateTimeOffset.UtcNow, false));
        }

        public async Task<IReadOnlyList<(long UserId, NotificationItem Item)>> AddManyAsync(
            IReadOnlyList<(long UserId, string Kind, string? Data, string DedupeKey)> items,
            CancellationToken cancellationToken)
        {
            var stored = new List<(long, NotificationItem)>();
            foreach (var (userId, kind, data, key) in items)
            {
                if (await AddAsync(userId, kind, data, key, cancellationToken) is { } item)
                {
                    stored.Add((userId, item));
                }
            }

            return stored;
        }

        public Task<NotificationPage> QueryAsync(long userId, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new NotificationPage([], 0));

        public Task MarkReadAsync(long userId, long upToId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingNotifier : IRealtimeNotifier
    {
        public List<NotificationItem> Pushed { get; } = [];

        public Task PushAsync(long userId, NotificationItem notification, CancellationToken cancellationToken)
        {
            Pushed.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNotifier : IRealtimeNotifier
    {
        public Task PushAsync(long userId, NotificationItem notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The hub is down.");
    }
}
