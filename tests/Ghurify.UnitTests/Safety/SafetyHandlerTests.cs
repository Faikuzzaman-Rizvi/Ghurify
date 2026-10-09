using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Ghurify.Application.Safety;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.UnitTests.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghurify.UnitTests.Safety;

/// <summary>SOS must get through whatever else fails; only the desk changes a destination, and a closure is queued once.</summary>
public sealed class SafetyHandlerTests
{
    private const long TravellerId = 1;
    private const long HostId = 2;
    private const long DeskId = 3;

    private readonly FakeAccessRepository _access = new();
    private readonly FakeSafetyRepository _safety = new();
    private readonly FakeAuditLog _audit = new();
    private readonly RecordingJobs _jobs = new();

    public SafetyHandlerTests()
    {
        _access.Add(TravellerId);
        _access.Add(HostId, [Role.Host]);
        _access.AddStaff(DeskId, StaffRoleDefaults.SafetyDesk);
    }

    // --- SOS -------------------------------------------------------------------------------

    [Fact]
    public async Task RaiseSos_WhenTheDeskPushAndTheTextBothFail_StillRecordsItAndTellsTheHost()
    {
        var notifications = new CountingNotificationStore();
        var handler = RaiseSos(new ThrowingBroadcaster(), new ThrowingSms(), notifications);

        var result = await handler.HandleAsync(TravellerId, 10, new RaiseSosCommand(23.1m, 92.2m, 20, "Help"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.EmergencyContactTexted);
        Assert.Single(result.Value.NearestHelp);
        Assert.Equal(1, _safety.SosRaised);
        Assert.Equal((HostId, "safety.sos"), Assert.Single(notifications.Stored));
    }

    [Fact]
    public async Task RaiseSos_ByTheHostThemselves_DoesNotNotifyTheHost()
    {
        var notifications = new CountingNotificationStore();
        var handler = RaiseSos(new ThrowingBroadcaster(), new ThrowingSms(), notifications);

        var result = await handler.HandleAsync(HostId, 10, new RaiseSosCommand(23.1m, 92.2m, null, null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(notifications.Stored);
    }

    [Theory]
    [InlineData(91, 90)]
    [InlineData(23, 181)]
    public async Task RaiseSos_WithAnImpossiblePosition_IsRefusedAndNothingIsRecorded(decimal latitude, decimal longitude)
    {
        var handler = RaiseSos(new ThrowingBroadcaster(), new ThrowingSms(), new CountingNotificationStore());

        var result = await handler.HandleAsync(TravellerId, 10, new RaiseSosCommand(latitude, longitude, null, null), CancellationToken.None);

        Assert.Equal("sos_position", result.Error?.Code);
        Assert.Equal(0, _safety.SosRaised);
    }

    [Fact]
    public async Task AcknowledgeSos_BySomeoneNotOnTheDesk_IsForbidden()
    {
        var handler = new SetSosStatusHandler(_safety, new AccessService(_access), _audit);

        var result = await handler.HandleAsync(HostId, 7, SosStatus.Acknowledged, CancellationToken.None);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(_audit.Entries);
    }

    // --- Destinations ----------------------------------------------------------------------

    [Fact]
    public async Task CloseDestination_ByTheDesk_IsAuditedAndQueuesTheClosureOnce()
    {
        var result = await ChangeStatus(DeskId, DestinationStatus.Closed, "Landslides.", "ভূমিধস।");

        Assert.True(result.Succeeded);
        Assert.Equal((DeskId, "destination.closed", "DestinationAlert", FakeSafetyRepository.AlertId), Assert.Single(_audit.Entries));
        Assert.Equal((typeof(ICloseDestinationJob), FakeSafetyRepository.AlertId), Assert.Single(_jobs.Queued));
    }

    [Fact]
    public async Task CautionOnADestination_QueuesNoClosure()
    {
        var result = await ChangeStatus(DeskId, DestinationStatus.Caution, "Heavy rain.", "ভারী বৃষ্টি।");

        Assert.True(result.Succeeded);
        Assert.Empty(_jobs.Queued);
    }

    [Fact]
    public async Task CloseDestination_WithoutABanglaNote_IsRefused()
    {
        var result = await ChangeStatus(DeskId, DestinationStatus.Closed, "Landslides.", " ");

        Assert.Equal("destination_note_required", result.Error?.Code);
        Assert.Empty(_jobs.Queued);
    }

    [Fact]
    public async Task CloseDestination_ByAHost_IsForbidden()
    {
        var result = await ChangeStatus(HostId, DestinationStatus.Closed, "Landslides.", "ভূমিধস।");

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(_audit.Entries);
        Assert.Empty(_jobs.Queued);
    }

    // --- Reports ---------------------------------------------------------------------------

    [Fact]
    public async Task FileDispute_AboutSomeoneElsesBooking_IsNotFound()
    {
        var handler = new FileReportHandler(_safety, NullLogger<FileReportHandler>.Instance);

        var result = await handler.HandleAsync(DeskId, new FileReportCommand(ReportKind.Dispute, 50, ReportReason.Payment, null), CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, _safety.ReportsFiled);
    }

    [Fact]
    public async Task FileDispute_ByTheTraveller_IsFiled()
    {
        var handler = new FileReportHandler(_safety, NullLogger<FileReportHandler>.Instance);

        var result = await handler.HandleAsync(TravellerId, new FileReportCommand(ReportKind.Dispute, 50, ReportReason.Payment, "No show."), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, _safety.ReportsFiled);
    }

    private RaiseSosHandler RaiseSos(ISafetyBroadcaster broadcaster, ISmsSender sms, CountingNotificationStore store) =>
        new(_safety, broadcaster, sms, new NotificationService(store, new SilentNotifier(), NullLogger<NotificationService>.Instance),
            NullLogger<RaiseSosHandler>.Instance);

    private Task<Result<Done>> ChangeStatus(long actorId, DestinationStatus status, string? note, string? noteBn) =>
        new ChangeDestinationStatusHandler(_safety, new AccessService(_access), _audit, _jobs, NullLogger<ChangeDestinationStatusHandler>.Instance)
            .HandleAsync(actorId, "sajek", new ChangeDestinationStatusCommand(status, note, noteBn), CancellationToken.None);

    // --- Fakes -----------------------------------------------------------------------------

    private sealed class FakeSafetyRepository : ISafetyRepository
    {
        public const long AlertId = 99;

        public int SosRaised { get; private set; }

        public int ReportsFiled { get; private set; }

        public Task<SosRaised?> AddSosAsync(NewSos sos, CancellationToken cancellationToken)
        {
            SosRaised++;
            var item = new SosBoardItem(1, sos.UserId, "Asha", sos.TripId, "Sajek", sos.Latitude, sos.Longitude, sos.Message,
                SosStatus.Open, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            HelpPoint[] help = [new(1, "Sajek police", "সাজেক থানা", null, 23.38, 92.29, 1200)];
            return Task.FromResult<SosRaised?>(new SosRaised(item, HostId, "Sister", "+8801711000111", help));
        }

        public Task<bool> SetSosStatusAsync(long sosId, SosStatus status, long actorId, long? ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<(long? AlertId, long? DestinationId)> SetDestinationStatusAsync(
            string slug, DestinationStatus status, string? note, string? noteBn, long actorId, CancellationToken cancellationToken) =>
            Task.FromResult<(long?, long?)>((AlertId, 5));

        public Task<DisputeBooking?> GetDisputeBookingAsync(long bookingId, CancellationToken cancellationToken) =>
            Task.FromResult<DisputeBooking?>(bookingId == 50 ? new DisputeBooking(50, TravellerId, HostId, 6120m) : null);

        public Task<long> AddReportAsync(NewReport report, CancellationToken cancellationToken) =>
            Task.FromResult((long)++ReportsFiled);

        public Task<SosBoardItem?> UpdateSosLocationAsync(long sosId, long userId, decimal latitude, decimal longitude, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SosBoardItem>> QuerySosBoardAsync(bool includeResolved, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long?> AddCheckInAsync(long tripId, long hostId, string label, DateTimeOffset dueAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CheckInView>> QueryCheckInsAsync(long tripId, long userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CompleteCheckInAsync(long checkInId, long userId, string? note, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MissedCheckIn>> SetMissedCheckInsAsync(DateTimeOffset now, int graceMinutes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MissedCheckIn>> QueryMissedCheckInsAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DestinationAlertRecord?> GetAlertAsync(long alertId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<long>> QueryTripsToCancelAsync(long destinationId, DateOnly today, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> MarkAlertProcessedAsync(long alertId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ReportView>> QueryReportsAsync(ReportKind? kind, ReportStatus status, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReportView?> ResolveReportAsync(long reportId, ReportStatus status, string resolution, long actorId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HidePostAsync(long postId, long actorId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> SuspendUserAsync(long userId, long actorId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DashboardCounts> GetDashboardAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<long>> QueryStaffAsync(IReadOnlyCollection<Role> roles, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingBroadcaster : ISafetyBroadcaster
    {
        public Task SosAsync(SosBoardItem sos, CancellationToken cancellationToken) => throw new InvalidOperationException("hub down");

        public Task CheckInMissedAsync(MissedCheckIn checkIn, CancellationToken cancellationToken) => throw new InvalidOperationException("hub down");
    }

    private sealed class ThrowingSms : ISmsSender
    {
        public Task<bool> SendAsync(PhoneNumber recipient, string text, CancellationToken cancellationToken) =>
            throw new HttpRequestException("gateway down");
    }

    private sealed class RecordingJobs : IBackgroundJobs
    {
        public List<(Type Job, long Id)> Queued { get; } = [];

        public Task EnqueueAsync<TJob>(long id, CancellationToken cancellationToken)
            where TJob : class, IIdJob
        {
            Queued.Add((typeof(TJob), id));
            return Task.CompletedTask;
        }
    }

    private sealed class CountingNotificationStore : INotificationRepository
    {
        public List<(long UserId, string Kind)> Stored { get; } = [];

        public Task<NotificationItem?> AddAsync(long userId, string kind, string? data, string dedupeKey, CancellationToken cancellationToken)
        {
            Stored.Add((userId, kind));
            return Task.FromResult<NotificationItem?>(new NotificationItem(Stored.Count, kind, data, DateTimeOffset.UtcNow, false));
        }

        public Task<IReadOnlyList<(long UserId, NotificationItem Item)>> AddManyAsync(
            IReadOnlyList<(long UserId, string Kind, string? Data, string DedupeKey)> items,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<NotificationPage> QueryAsync(long userId, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new NotificationPage([], 0));

        public Task MarkReadAsync(long userId, long upToId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SilentNotifier : IRealtimeNotifier
    {
        public Task PushAsync(long userId, NotificationItem notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
