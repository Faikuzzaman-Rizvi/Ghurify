using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dapper;
using Ghurify.Application.Identity;
using Ghurify.Application.Social;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Identity photos and profile pictures end to end against a real database, with storage in
/// memory: upload, the check, an admin looking at the photos (audited), and who may not.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class IdentityDocumentEndpointTests(SqlServerFixture database)
{
    private const string Gps = "GPS 22.3569 N 91.7832 E";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadBothSidesOfAnNid_Submit_AndAnAdminSeesThePhotos_WithAnAuditEntry()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var admin = await data.CreateAdminAsync();
        var outsider = await data.CreateVerifiedTravelerAsync();
        var documents = new InMemoryDocumentStorage();
        await using var api = Api(documents, new InMemoryMediaStorage());
        using var client = TestData.ClientFor(api, user);

        var front = await UploadAsync(client, documents, "NidFront");
        var back = await UploadAsync(client, documents, "NidBack");

        // The stored copies carry no location, and the raw uploads are gone.
        Assert.All(documents.Blobs, blob =>
        {
            Assert.EndsWith(".jpg", blob.Key, StringComparison.Ordinal);
            Assert.DoesNotContain(Gps, Encoding.ASCII.GetString(blob.Value), StringComparison.Ordinal);
        });

        // …999 makes the fake provider answer Pending, so the check waits for an admin.
        using var started = await client.PostAsJsonAsync(
            "/api/v1/me/verification",
            new { level = "Nid", idNumber = "4444444999", dateOfBirth = "1995-04-12", documentIds = new[] { front, back } },
            Token);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var verificationId = (await started.Content.ReadFromJsonAsync<IdResponse>(TestData.Json, Token))!.Id;

        using var outsiderClient = TestData.ClientFor(api, outsider);
        using var refused = await outsiderClient.GetAsync(new Uri($"/api/v1/admin/verifications/{verificationId}/documents", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var adminClient = TestData.ClientFor(api, admin);
        var photos = await adminClient.GetFromJsonAsync<List<DocumentResponse>>($"/api/v1/admin/verifications/{verificationId}/documents", TestData.Json, Token);
        Assert.Equal(["NidFront", "NidBack"], photos!.Select(photo => photo.Kind));
        Assert.All(photos!, photo => Assert.Contains("sig=read", photo.Url, StringComparison.Ordinal));

        var queue = await adminClient.GetFromJsonAsync<QueueResponse>("/api/v1/admin/verifications?status=Pending", TestData.Json, Token);
        Assert.Equal(2, queue!.Items.Single(item => item.Id == verificationId).DocumentCount);

        await using var connection = await data.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM [Safety].[AuditLog]
            WHERE [Action] = 'verification.documents_viewed' AND [EntityId] = @Id AND [ActorId] = @ActorId;
            """,
            new { Id = verificationId, ActorId = admin.Id },
            cancellationToken: Token)));
    }

    [Fact]
    public async Task SubmittingSomeoneElsesUploadedPhoto_IsRefused()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var other = await data.CreateUserAsync();
        await using var api = Api(new InMemoryDocumentStorage(), new InMemoryMediaStorage());
        var theirs = await ProfileEndpointTests.ReadyDocumentsAsync(data, other.Id, VerificationDocumentKind.NidFront, VerificationDocumentKind.NidBack);

        using var client = TestData.ClientFor(api, user);
        using var response = await client.PostAsJsonAsync(
            "/api/v1/me/verification", new { level = "Nid", idNumber = "1234567891", dateOfBirth = "1995-04-12", documentIds = theirs }, Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_IsUploaded_ServedToAnyone_AndRemovable()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var media = new InMemoryMediaStorage();
        await using var api = Api(new InMemoryDocumentStorage(), media);
        using var client = TestData.ClientFor(api, user);
        using var anonymous = api.CreateClientWithoutRedirects();

        using var before = await anonymous.GetAsync(new Uri($"/api/v1/users/{user.Id}/avatar", UriKind.Relative), Token);
        // No picture: 204 (the page shows initials), never a 404 that the browser reports as an error.
        Assert.Equal(HttpStatusCode.NoContent, before.StatusCode);

        using var ticketResponse = await client.PostAsJsonAsync("/api/v1/me/avatar", new { contentType = "image/jpeg", sizeBytes = 2000 }, Token);
        var ticket = (await ticketResponse.Content.ReadFromJsonAsync<TicketResponse>(TestData.Json, Token))!;
        media.Put(ticket.UploadUrl, JpegWithGps());
        using var completed = await client.PostAsJsonAsync("/api/v1/me/avatar/complete", new { uploadId = ticket.Id }, Token);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        using var served = await anonymous.GetAsync(new Uri($"/api/v1/users/{user.Id}/avatar", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Redirect, served.StatusCode);
        Assert.Contains($"avatars/{user.Id}/", served.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Gps, Encoding.ASCII.GetString(media.Blobs.Single().Value), StringComparison.Ordinal);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me/profile", TestData.Json, Token);
        Assert.NotNull(profile!.AvatarVersion);

        using var removed = await client.DeleteAsync(new Uri("/api/v1/me/avatar", UriKind.Relative), Token);
        using var after = await anonymous.GetAsync(new Uri($"/api/v1/users/{user.Id}/avatar", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, after.StatusCode);
        Assert.Empty(media.Blobs);

        // No picture, no version: the page shows the initial without asking for one.
        var afterRemoval = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me/profile", TestData.Json, Token);
        Assert.Null(afterRemoval!.AvatarVersion);
    }

    [Fact]
    public async Task ProfilePicture_WhenStorageIsUnreachable_AnswersStorageUnavailable_NotAServerError()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var media = new InMemoryMediaStorage();
        await using var api = Api(new InMemoryDocumentStorage(), media);
        using var client = TestData.ClientFor(api, user);

        using var ticketResponse = await client.PostAsJsonAsync("/api/v1/me/avatar", new { contentType = "image/jpeg", sizeBytes = 2000 }, Token);
        var ticket = (await ticketResponse.Content.ReadFromJsonAsync<TicketResponse>(TestData.Json, Token))!;
        media.Put(ticket.UploadUrl, JpegWithGps());
        media.Unreachable = true;

        using var completed = await client.PostAsJsonAsync("/api/v1/me/avatar/complete", new { uploadId = ticket.Id }, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, completed.StatusCode);
        Assert.Equal("storage_unavailable", (await completed.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token))!.Code);
        Assert.True(completed.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task RemovingSomeoneElsesProfilePicture_NeedsAModerator()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        await using var api = Api(new InMemoryDocumentStorage(), new InMemoryMediaStorage());

        using var client = TestData.ClientFor(api, traveller);
        using var response = await client.DeleteAsync(new Uri($"/api/v1/admin/users/{user.Id}/avatar", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private GhurifyApiFactory Api(InMemoryDocumentStorage documents, InMemoryMediaStorage media) => new(database.ConnectionString)
    {
        ReplaceServices = services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IIdentityDocumentStorage>(documents));
            services.Replace(ServiceDescriptor.Singleton<IMediaStorage>(media));
        },
    };

    private static async Task<long> UploadAsync(HttpClient client, InMemoryDocumentStorage storage, string kind)
    {
        var photo = JpegWithGps();
        using var started = await client.PostAsJsonAsync(
            "/api/v1/me/verification/documents", new { kind, contentType = "image/jpeg", sizeBytes = photo.Length }, Token);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var ticket = (await started.Content.ReadFromJsonAsync<TicketResponse>(TestData.Json, Token))!;

        storage.Put(ticket.UploadUrl, photo);

        using var completed = await client.PostAsync(new Uri($"/api/v1/me/verification/documents/{ticket.Id}/complete", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        return long.Parse(ticket.Id, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A minimal JPEG whose EXIF block carries a GPS position.</summary>
    private static byte[] JpegWithGps()
    {
        byte[] exifPayload = [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(Gps)];
        var exif = new byte[4 + exifPayload.Length];
        exif[0] = 0xFF;
        exif[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(exif.AsSpan(2), (ushort)(exifPayload.Length + 2));
        exifPayload.CopyTo(exif, 4);

        return [0xFF, 0xD8, .. exif, 0xFF, 0xDA, 0x00, 0x04, 0x01, 0x02, 0x10, 0x20, 0xFF, 0xD9];
    }

    private sealed record TicketResponse(string Id, string UploadUrl);

    private sealed record IdResponse(long Id);

    private sealed record DocumentResponse(long Id, string Kind, string Url);

    private sealed record QueueResponse(List<QueueItem> Items);

    private sealed record QueueItem(long Id, int DocumentCount);

    private sealed record ProfileResponse(long? AvatarVersion);

    private sealed record Problem(string? Code);
}
