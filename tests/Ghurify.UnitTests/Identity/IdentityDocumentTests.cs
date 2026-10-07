using System.Buffers.Binary;
using System.Text;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Social;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Identity photos and profile pictures: what each check needs, that uploads are really images
/// and lose their location data, that only admins see ID photos (and are audited), that nobody
/// can submit or claim someone else's upload, and that old photos are deleted.
/// </summary>
public sealed class IdentityDocumentTests
{
    private const long UserId = 1;
    private const long OtherId = 2;
    private const long AdminId = 99;
    private const string Gps = "GPS 23.8103 N 90.4125 E";

    private readonly FakeAccessRepository _access = new();
    private readonly FakeProfileRepository _profiles = new();
    private readonly FakeVerificationRepository _verifications = new();
    private readonly FakeVerificationDocumentRepository _documents = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero));
    private readonly IOptions<MediaOptions> _media = Options.Create(new MediaOptions());

    public IdentityDocumentTests()
    {
        _access.Add(UserId);
        _access.Add(OtherId);
        _access.Add(AdminId, [Role.Admin]);
        _profiles.Add(UserId);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- What a check needs ----------------------------------------------------------------

    [Fact]
    public void Requirements_ANidCheckNeedsBothSides_AndTheHostLevelAddsTheSelfie()
    {
        Assert.Equal(
            [VerificationDocumentKind.NidFront, VerificationDocumentKind.NidBack],
            VerificationRequirements.Missing(IdDocumentType.Nid, VerificationLevel.Nid, []));
        Assert.Equal(
            [VerificationDocumentKind.Selfie],
            VerificationRequirements.Missing(
                IdDocumentType.Nid, VerificationLevel.NidSelfie, [VerificationDocumentKind.NidFront, VerificationDocumentKind.NidBack]));
        Assert.Empty(VerificationRequirements.Missing(IdDocumentType.Passport, VerificationLevel.Nid, [VerificationDocumentKind.PassportPhotoPage]));
        Assert.False(VerificationRequirements.Fits(IdDocumentType.Passport, VerificationDocumentKind.NidFront));
        Assert.True(VerificationRequirements.Fits(IdDocumentType.Passport, VerificationDocumentKind.ProfessionalLicence));
    }

    [Theory]
    [InlineData(IdDocumentType.Passport, "eb 0123456", "EB0123456")]
    [InlineData(IdDocumentType.DrivingLicence, "DK-0123456-C00001", "DK0123456C00001")]
    public void DocumentNumber_IsNormalised(IdDocumentType type, string typed, string expected)
    {
        Assert.True(DocumentNumber.TryNormalise(type, typed, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData(IdDocumentType.Passport, "ABCDEFGH")]
    [InlineData(IdDocumentType.Passport, "EB01;DROP")]
    [InlineData(IdDocumentType.DrivingLicence, "123")]
    public void DocumentNumber_RefusesWhatCannotBeANumber(IdDocumentType type, string typed) =>
        Assert.False(DocumentNumber.TryNormalise(type, typed, out _));

    // --- Submitting a check ----------------------------------------------------------------

    [Fact]
    public async Task StartVerification_WithoutTheBackOfTheNid_SaysWhatIsMissing()
    {
        var front = _documents.AddReady(UserId, VerificationDocumentKind.NidFront);

        var result = await Start(VerificationLevel.Nid, IdDocumentType.Nid, "1234567890", [front]);

        Assert.Equal("documents_missing", result.Error?.Code);
        Assert.Contains("NidBack", result.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(_verifications.Checks);
    }

    [Fact]
    public async Task StartVerification_WithSomeoneElsesPhoto_IsRefused()
    {
        var front = _documents.AddReady(UserId, VerificationDocumentKind.NidFront);
        var theirs = _documents.AddReady(OtherId, VerificationDocumentKind.NidBack);

        var result = await Start(VerificationLevel.Nid, IdDocumentType.Nid, "1234567890", [front, theirs]);

        Assert.Equal("documents_not_ready", result.Error?.Code);
        Assert.Empty(_verifications.Checks);
    }

    [Fact]
    public async Task StartVerification_WithAPassport_GoesToAnAdmin_WithoutTheProvider()
    {
        var page = _documents.AddReady(UserId, VerificationDocumentKind.PassportPhotoPage);
        var provider = new FakeEkyc();

        var result = await Start(VerificationLevel.Nid, IdDocumentType.Passport, "EB0123456", [page], provider);

        Assert.True(result.Succeeded);
        Assert.Empty(provider.Requests);
        var check = _verifications.Checks.Single().Check;
        Assert.Equal(VerificationStatus.Pending, check.Status);
        Assert.Equal(StartVerificationHandler.ManualReview, check.Provider);
        Assert.Equal(IdDocumentType.Passport, check.IdType);
        Assert.Equal([page], check.DocumentIds);
    }

    [Fact]
    public async Task StartVerification_ForTheHostLevel_NeedsTheSelfie()
    {
        long[] sides = [_documents.AddReady(UserId, VerificationDocumentKind.NidFront), _documents.AddReady(UserId, VerificationDocumentKind.NidBack)];

        var result = await Start(VerificationLevel.NidSelfie, IdDocumentType.Nid, "1234567890", sides);

        Assert.Equal("documents_missing", result.Error?.Code);
    }

    // --- Uploading a photo -----------------------------------------------------------------

    [Fact]
    public async Task CompleteUpload_KeepsAClean_Copy_WithoutTheLocation_AndDeletesTheUpload()
    {
        var ticket = await StartUpload(VerificationDocumentKind.NidFront, "image/jpeg");
        var id = long.Parse(ticket.Value!.Id, System.Globalization.CultureInfo.InvariantCulture);
        var upload = _documents.Documents.Single().UploadBlob;
        _storage.Blobs[upload] = (Jpeg(withExif: true), "image/jpeg");

        var result = await Complete(id);

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationDocumentStatus.Ready, result.Value!.Status);
        Assert.False(_storage.Blobs.ContainsKey(upload));
        var stored = _storage.Blobs[_documents.Documents.Single().Blob!].Bytes;
        Assert.DoesNotContain(Gps, Encoding.ASCII.GetString(stored), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteUpload_OfAFileThatIsNotWhatItClaims_IsRefused_AndDeleted()
    {
        var ticket = await StartUpload(VerificationDocumentKind.NidFront, "image/png");
        var id = long.Parse(ticket.Value!.Id, System.Globalization.CultureInfo.InvariantCulture);
        var upload = _documents.Documents.Single().UploadBlob;
        _storage.Blobs[upload] = (Jpeg(withExif: false), "image/png");

        var result = await Complete(id);

        Assert.Equal("upload_not_image", result.Error?.Code);
        Assert.Equal(VerificationDocumentStatus.Rejected, _documents.Documents.Single().Status);
        Assert.Empty(_storage.Blobs);
    }

    [Fact]
    public async Task StartUpload_RefusesAnythingButPhotos()
    {
        var result = await StartUpload(VerificationDocumentKind.NidFront, "application/pdf");

        Assert.Equal("media_type", result.Error?.Code);
    }

    [Fact]
    public async Task CompleteUpload_OfSomeoneElsesDocument_IsNotFound()
    {
        var ticket = await StartUpload(VerificationDocumentKind.NidFront, "image/jpeg");
        var id = long.Parse(ticket.Value!.Id, System.Globalization.CultureInfo.InvariantCulture);

        var result = await new CompleteDocumentUploadHandler(_documents, _storage, _media, NullLogger<CompleteDocumentUploadHandler>.Instance)
            .HandleAsync(OtherId, id, Token);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    // --- Reviewing -------------------------------------------------------------------------

    [Fact]
    public async Task ReviewDocuments_ByANonAdmin_IsForbidden_AndNothingIsShown()
    {
        var result = await Review(OtherId, verificationId: 5);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ReviewDocuments_ByAnAdmin_GivesShortLivedLinks_AndIsAudited()
    {
        var front = _documents.AddReady(UserId, VerificationDocumentKind.NidFront);
        _documents.Submit(front, verificationId: 5);

        var result = await Review(AdminId, verificationId: 5);

        var view = Assert.Single(result.Value!);
        Assert.Contains($"until={_clock.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}", view.Url, StringComparison.Ordinal);
        Assert.Equal((AdminId, "verification.documents_viewed", "Verification", 5L), Assert.Single(_audit.Entries));
    }

    [Fact]
    public async Task Purge_DeletesUploadsNeverSubmitted_AfterSevenDays()
    {
        var old = _documents.AddReady(UserId, VerificationDocumentKind.NidFront);
        _documents.Documents[0] = _documents.Documents[0] with { Created = _clock.UtcNow.AddDays(-8) };
        _storage.Blobs[_documents.Documents[0].Blob!] = ([1, 2, 3], "image/jpeg");

        var purged = await new PurgeVerificationDocumentsHandler(_documents, _storage, _clock, NullLogger<PurgeVerificationDocumentsHandler>.Instance)
            .HandleAsync(Token);

        Assert.Equal(1, purged);
        Assert.Empty(_storage.Blobs);
        Assert.Equal(VerificationDocumentStatus.Purged, _documents.Documents.Single(document => document.Id == old).Status);
    }

    // --- Profile pictures ------------------------------------------------------------------

    [Fact]
    public async Task Avatar_Complete_StoresACleanPicture_AndDeletesTheOldOne()
    {
        _profiles.Avatars[UserId] = "avatars/1/old.jpg";
        _storage.Blobs["avatars/1/old.jpg"] = ([1], "image/jpeg");
        var ticket = await StartAvatar(UserId);
        _storage.Blobs[$"avatars/1/{ticket.Value!.Id}.upload"] = (Jpeg(withExif: true), "image/jpeg");

        var result = await CompleteAvatar(UserId, ticket.Value.Id);

        Assert.True(result.Succeeded);
        Assert.Equal($"avatars/1/{ticket.Value.Id}.jpg", _profiles.Avatars[UserId]);
        Assert.Equal([$"avatars/1/{ticket.Value.Id}.jpg"], _storage.Blobs.Keys);
    }

    [Fact]
    public async Task Avatar_SomeoneElsesUpload_CannotBeClaimed()
    {
        var ticket = await StartAvatar(UserId);
        _storage.Blobs[$"avatars/1/{ticket.Value!.Id}.upload"] = (Jpeg(withExif: false), "image/jpeg");

        var result = await CompleteAvatar(OtherId, ticket.Value.Id);

        Assert.Equal("upload_missing", result.Error?.Code);
        Assert.False(_profiles.Avatars.ContainsKey(OtherId));
    }

    [Fact]
    public async Task Avatar_RemovedByAnotherTraveller_IsForbidden_ButAModeratorCan_WithAnAuditEntry()
    {
        _access.Add(50, [Role.Moderator]);
        _profiles.Avatars[UserId] = "avatars/1/rude.jpg";
        var handler = new RemoveAvatarHandler(_storage, _profiles, new AccessService(_access), _audit);

        Assert.Equal(ErrorKind.Forbidden, (await handler.HandleAsync(OtherId, UserId, Token)).Error?.Kind);
        Assert.True((await handler.HandleAsync(50, UserId, Token)).Succeeded);
        Assert.False(_profiles.Avatars.ContainsKey(UserId));
        Assert.Equal((50L, "user.avatar_removed", "User", UserId), Assert.Single(_audit.Entries));
    }

    // --- Wiring ----------------------------------------------------------------------------

    private Task<Result<VerificationRecord>> Start(
        VerificationLevel level, IdDocumentType type, string number, long[] documents, FakeEkyc? provider = null) =>
        new StartVerificationHandler(
                _verifications, _documents, _profiles, provider ?? new FakeEkyc(), new FakeNidHasher(), new AccessService(_access),
                NullLogger<StartVerificationHandler>.Instance)
            .HandleAsync(UserId, new StartVerificationCommand(level, number, new DateOnly(1995, 4, 12), documents, type), Token);

    private Task<Result<UploadTicket>> StartUpload(VerificationDocumentKind kind, string contentType) =>
        new StartDocumentUploadHandler(_documents, _storage, new AccessService(_access), _clock, _media)
            .HandleAsync(UserId, new StartDocumentUploadCommand(kind, contentType, 50_000), Token);

    private Task<Result<MyDocumentView>> Complete(long id) =>
        new CompleteDocumentUploadHandler(_documents, _storage, _media, NullLogger<CompleteDocumentUploadHandler>.Instance)
            .HandleAsync(UserId, id, Token);

    private Task<Result<IReadOnlyList<ReviewDocumentView>>> Review(long actorId, long verificationId) =>
        new GetVerificationDocumentsHandler(_documents, _storage, new AccessService(_access), _audit, _clock)
            .HandleAsync(actorId, verificationId, Token);

    private Task<Result<UploadTicket>> StartAvatar(long userId) =>
        new StartAvatarUploadHandler(_storage, new AccessService(_access), _clock, _media).HandleAsync(userId, "image/jpeg", 40_000, Token);

    private Task<Result<Done>> CompleteAvatar(long userId, string uploadId) =>
        new CompleteAvatarUploadHandler(_storage, _profiles, NullLogger<CompleteAvatarUploadHandler>.Instance)
            .HandleAsync(userId, new CompleteAvatarCommand(uploadId), Token);

    private static byte[] Jpeg(bool withExif) => [
        0xFF, 0xD8,
        .. Segment(0xE0, [.. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0]),
        .. withExif ? Segment(0xE1, [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(Gps)]) : [],
        .. Segment(0xDB, new byte[65]),
        0xFF, 0xDA, 0x00, 0x04, 0x01, 0x02, 0x10, 0x20, 0x30, 0xFF, 0xD9,
    ];

    private static byte[] Segment(byte marker, byte[] payload)
    {
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2, 2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        return segment;
    }
}
