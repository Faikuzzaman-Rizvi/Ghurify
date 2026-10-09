using Ghurify.Application.Abstractions;
using Ghurify.Application.Admin;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.UnitTests.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghurify.UnitTests.Admin;

/// <summary>The admin desk's rules: admins only, a reason for every change, no self-harm, valid places.</summary>
public sealed class AdminHandlerTests
{
    private const long AdminId = 1;
    private const long OtherAdminId = 2;
    private const long TravellerId = 3;
    private const long DeskId = 4;

    private readonly FakeAccessRepository _access = new();
    private readonly FakeAdminRepository _admin = new();
    private readonly FakeAuditLog _audit = new();

    public AdminHandlerTests()
    {
        _access.AddStaff(AdminId, StaffRoleDefaults.Admin);
        _access.AddStaff(OtherAdminId, StaffRoleDefaults.Admin);
        _access.Add(TravellerId);
        _access.AddStaff(DeskId, StaffRoleDefaults.SafetyDesk);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SuspendUser_WithAReason_ChangesTheStatus_AndIsAudited()
    {
        var result = await SetStatus(AdminId, TravellerId, UserStatus.Suspended, "Harassment reports.");

        Assert.True(result.Succeeded);
        Assert.Equal(UserStatus.Suspended, _admin.Statuses[TravellerId]);
        Assert.Equal((AdminId, "user.suspended", "User", TravellerId), Assert.Single(_audit.Entries));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task SuspendUser_WithoutAReason_IsRefused(string? reason)
    {
        var result = await SetStatus(AdminId, TravellerId, UserStatus.Suspended, reason);

        Assert.Equal("reason_required", result.Error?.Code);
        Assert.Empty(_admin.Statuses);
    }

    [Fact]
    public async Task SuspendUser_ByANonAdmin_IsForbidden()
    {
        var result = await SetStatus(DeskId, TravellerId, UserStatus.Suspended, "Because.");

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task SuspendUser_ThemselvesOrAnotherStaffMember_IsRefused()
    {
        Assert.Equal("own_account", (await SetStatus(AdminId, AdminId, UserStatus.Suspended, "Oops.")).Error?.Code);
        Assert.Equal("target_is_staff", (await SetStatus(AdminId, OtherAdminId, UserStatus.Suspended, "Oops.")).Error?.Code);
        Assert.Empty(_admin.Statuses);
    }

    [Fact]
    public async Task SuspendUser_WithoutTheSuspendPermission_IsForbidden()
    {
        // Somebody who may look people up but not act on them.
        _access.AddStaff(DeskId, StaffRoleDefaults.SafetyDesk);

        Assert.Equal("forbidden", (await SetStatus(DeskId, TravellerId, UserStatus.Suspended, "No.")).Error?.Code);
        Assert.Empty(_admin.Statuses);
    }

    [Fact]
    public async Task SetStatus_ToPendingEmail_IsNotAllowedHere()
    {
        Assert.Equal("user_status", (await SetStatus(AdminId, TravellerId, UserStatus.PendingEmail, "No.")).Error?.Code);
    }

    [Theory]
    [InlineData("Sajek Valley")]
    [InlineData("-sajek")]
    [InlineData("ab")]
    public async Task SaveDestination_WithABadSlug_IsRefused(string slug)
    {
        var result = await SaveDestination(Destination(slug));

        Assert.Equal("destination_slug", result.Error?.Code);
        Assert.Empty(_admin.Destinations);
    }

    [Fact]
    public async Task SaveDestination_OutsideBangladesh_OrWithHalfAPosition_IsRefused()
    {
        Assert.Equal("destination_position", (await SaveDestination(Destination("kathmandu") with { Latitude = 27.7m, Longitude = 85.3m })).Error?.Code);
        Assert.Equal("destination_position", (await SaveDestination(Destination("half") with { Longitude = null })).Error?.Code);
    }

    [Fact]
    public async Task SaveDestination_MissingTheBanglaText_IsRefused()
    {
        Assert.Equal("destination_text", (await SaveDestination(Destination("sajek-valley") with { SummaryBn = " " })).Error?.Code);
    }

    [Fact]
    public async Task SaveDestination_Valid_IsTrimmedSavedAndAudited()
    {
        var result = await SaveDestination(Destination("sajek-valley") with { Name = "  Sajek Valley  " });

        Assert.True(result.Value!.Added);
        Assert.Equal("Sajek Valley", _admin.Destinations.Single().Name);
        Assert.Equal("destination.added", Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public async Task SaveEmergencyPoint_ByTheDesk_WithABadPhone_IsRefused()
    {
        var handler = new SaveEmergencyPointHandler(_admin, new AccessService(_access), _audit);

        var result = await handler.HandleAsync(DeskId, new EmergencyPointEdit(null, null, 2, "Clinic", "ক্লিনিক", "call me", 23.8m, 90.4m, true), Token);

        Assert.Equal("point_phone", result.Error?.Code);
    }

    [Fact]
    public async Task SaveEmergencyPoint_ByATraveller_IsForbidden()
    {
        var handler = new SaveEmergencyPointHandler(_admin, new AccessService(_access), _audit);

        var result = await handler.HandleAsync(TravellerId, new EmergencyPointEdit(null, null, 2, "Clinic", "ক্লিনিক", null, 23.8m, 90.4m, true), Token);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    private Task<Result<Done>> SetStatus(long actorId, long userId, UserStatus status, string? reason) =>
        new SetUserStatusHandler(_admin, new AccessService(_access), _audit, NullLogger<SetUserStatusHandler>.Instance)
            .HandleAsync(actorId, userId, new SetUserStatusCommand(status, reason), Token);

    private Task<Result<DestinationSaved>> SaveDestination(DestinationEdit edit) =>
        new SaveDestinationHandler(_admin, new AccessService(_access), _audit).HandleAsync(AdminId, edit, Token);

    private static DestinationEdit Destination(string slug) =>
        new(slug, "Sajek", "সাজেক", "Chattogram", "চট্টগ্রাম", "Above the clouds.", "মেঘের ওপরে।", DestinationKind.Hills, 23.38m, 92.29m);

    private sealed class FakeAdminRepository : IAdminRepository
    {
        public Dictionary<long, UserStatus> Statuses { get; } = [];

        public List<DestinationEdit> Destinations { get; } = [];

        public Task<StatusChange> SetUserStatusAsync(long userId, UserStatus status, long actorId, CancellationToken cancellationToken)
        {
            Statuses[userId] = status;
            return Task.FromResult(StatusChange.Changed);
        }

        public Task<bool> SetDestinationAsync(DestinationEdit edit, long actorId, CancellationToken cancellationToken)
        {
            Destinations.Add(edit);
            return Task.FromResult(true);
        }

        public Task<long?> SetEmergencyPointAsync(EmergencyPointEdit edit, long actorId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(1);

        public Task<AdminUserPage> SearchUsersAsync(string? search, UserStatus? status, Role? role, int offset, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdminUserDetail?> GetUserAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RequirePasswordResetAsync(long userId, long actorId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AdminTripPage> SearchTripsAsync(string? search, TripStatus? status, int offset, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdminBookingDetail?> GetBookingAsync(long? bookingId, string? reference, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EmergencyPointView>> QueryEmergencyPointsAsync(string? destinationSlug, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
