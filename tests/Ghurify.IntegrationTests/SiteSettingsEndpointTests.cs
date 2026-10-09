using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Site;
using Ghurify.IntegrationTests.Infrastructure;

namespace Ghurify.IntegrationTests;

/// <summary>
/// The site's own settings against a real database: the public configuration, saving branding
/// and theme, the readability guard, and the uploaded images.
///
/// Every test puts the settings back, because they are global rather than owned by a test user:
/// one left behind would change what every other test sees.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class SiteSettingsEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A 1x1 PNG, which is a real image as far as the format check is concerned.</summary>
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    [Fact]
    public async Task TheConfiguration_IsPublic_AndIsWhatTheAppShippedWith()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var anonymous = api.CreateClientWithoutCookieJar();

        var config = await anonymous.GetFromJsonAsync<Config>("/api/v1/site/config", TestData.Json, Token);

        Assert.NotNull(config);
        // The defaults are the values that used to be hard-coded, so an untouched database
        // renders exactly the site the code shipped with.
        Assert.Equal("Ghurify", config.Identity.Name);
        Assert.Equal("#245c43", config.Theme.Colours["hill"]);
        Assert.Equal("Barlow", config.Theme.BodyFont);
        // Nothing uploaded, so the configuration points at the app's own files.
        Assert.All(config.Assets, asset => Assert.False(asset.IsCustom));
    }

    [Fact]
    public async Task TheConfiguration_AnswersNotModifiedWhenTheBrowserAlreadyHasIt()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var anonymous = api.CreateClientWithoutCookieJar();

        using var first = await anonymous.GetAsync(new Uri("/api/v1/site/config", UriKind.Relative), Token);
        var tag = first.Headers.ETag;
        Assert.NotNull(tag);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/site/config");
        request.Headers.IfNoneMatch.Add(tag);
        using var second = await anonymous.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Branding_IsSaved_AndTheWholeSiteFollows()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);
        using var anonymous = api.CreateClientWithoutCookieJar();

        try
        {
            using var saved = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new
                {
                    settings = new Dictionary<string, string>
                    {
                        [SiteSettingKeys.Name] = "Bhromon",
                        [SiteSettingKeys.Tagline] = "Go further, together",
                        [SiteSettingKeys.ContactEmail] = "hello@bhromon.test",
                        [SiteSettingKeys.SocialFacebook] = "https://facebook.com/bhromon",
                    },
                },
                Token);

            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var result = await saved.Content.ReadFromJsonAsync<Saved>(TestData.Json, Token);
            Assert.Equal(4, result!.Changed);

            var config = await anonymous.GetFromJsonAsync<Config>("/api/v1/site/config", TestData.Json, Token);
            Assert.Equal("Bhromon", config!.Identity.Name);
            // The Bangla name was not touched, so it stays on its default.
            Assert.Equal("ঘুরিফাই", config.Identity.NameBn);
            Assert.Equal("hello@bhromon.test", config.Contact.Email);
            Assert.Equal("facebook", Assert.Single(config.Social).Platform);

            // Saving the same values again moves nothing, so there is no history and no audit noise.
            using var again = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new { settings = new Dictionary<string, string> { [SiteSettingKeys.Name] = "Bhromon" } },
                Token);
            var unchanged = await again.Content.ReadFromJsonAsync<Saved>(TestData.Json, Token);
            Assert.Equal(0, unchanged!.Changed);

            Assert.Equal(1, await AuditCountAsync(data, "site.settings.update", superAdmin.Id));

            var history = await client.GetFromJsonAsync<List<HistoryItem>>(
                $"/api/v1/admin/settings/history/{SiteSettingKeys.Name}", TestData.Json, Token);
            var entry = Assert.Single(history!);
            // Null on the old side means it had been on its default until now.
            Assert.Null(entry.OldValue);
            Assert.Equal("Bhromon", entry.NewValue);
        }
        finally
        {
            await ResetAsync(data);
        }
    }

    [Fact]
    public async Task AnEmptyValue_PutsASettingBackOnItsDefault()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);

        try
        {
            await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new { settings = new Dictionary<string, string> { [SiteSettingKeys.Name] = "Bhromon" } },
                Token);

            using var reset = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new { settings = new Dictionary<string, string> { [SiteSettingKeys.Name] = "" } },
                Token);
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

            var config = await client.GetFromJsonAsync<Config>("/api/v1/site/config", TestData.Json, Token);
            Assert.Equal("Ghurify", config!.Identity.Name);

            // The row is gone rather than holding a copy of the default, which is what keeps
            // "is this customised?" answerable.
            await using var connection = await data.OpenAsync();
            var rows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM [Site].[Setting] WHERE [Key] = @Key AND [Archived] = 0;",
                new { Key = SiteSettingKeys.Name },
                cancellationToken: Token));
            Assert.Equal(0, rows);
        }
        finally
        {
            await ResetAsync(data);
        }
    }

    [Fact]
    public async Task AnUnreadableTheme_IsRefused_AndNothingIsSaved()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);

        try
        {
            using var refused = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new
                {
                    settings = new Dictionary<string, string>
                    {
                        // A pale mint primary: unreadable as text on a white page.
                        [SiteSettingKeys.ColourHill] = "#e8f5ee",
                        // Valid on its own, and it must not slip through beside the bad one.
                        [SiteSettingKeys.ColourJamdani] = "#7d1f3f",
                    },
                },
                Token);

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var problem = await refused.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
            Assert.Equal("setting_low_contrast", problem!.Code);

            // All or nothing: the acceptable colour in the same batch was not saved either.
            var config = await client.GetFromJsonAsync<Config>("/api/v1/site/config", TestData.Json, Token);
            Assert.Equal("#245c43", config!.Theme.Colours["hill"]);
            Assert.Equal("#a3305c", config.Theme.Colours["jamdani"]);
        }
        finally
        {
            await ResetAsync(data);
        }
    }

    [Fact]
    public async Task BrandingAndTheme_AreSeparatePermissions()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        // A role that may rename the site but not recolour it.
        var brandingOnly = await data.CreateUserAsync(Gender.Female, "Brand manager");
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        var roleId = await CreateRoleAsync(data, "brand-only", [Permissions.SettingsBranding]);
        await GrantAsync(data, brandingOnly.Id, roleId, superAdmin.Id);

        try
        {
            using var client = TestData.ClientFor(api, brandingOnly);

            var view = await client.GetFromJsonAsync<SettingsPage>(
                "/api/v1/admin/settings", TestData.Json, Token);
            Assert.Contains("Identity", view!.EditableGroups);
            Assert.DoesNotContain("Theme", view.EditableGroups);

            using var renamed = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new { settings = new Dictionary<string, string> { [SiteSettingKeys.Tagline] = "A new line" } },
                Token);
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

            using var recoloured = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new { settings = new Dictionary<string, string> { [SiteSettingKeys.ColourHill] = "#1f4e7a" } },
                Token);
            Assert.Equal(HttpStatusCode.Forbidden, recoloured.StatusCode);
        }
        finally
        {
            await ResetAsync(data);
            await DeleteRoleAsync(data, roleId);
        }
    }

    [Fact]
    public async Task Settings_AreRefusedToEveryoneWithoutAPermission()
    {
        await using var data = new TestData(database.ConnectionString);
        var traveller = await data.CreateVerifiedTravelerAsync();
        var desk = await data.CreateSafetyDeskAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        foreach (var user in new[] { traveller, desk })
        {
            using var client = TestData.ClientFor(api, user);

            using var read = await client.GetAsync(new Uri("/api/v1/admin/settings", UriKind.Relative), Token);
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

            using var write = await client.PutAsJsonAsync(
                "/api/v1/admin/settings",
                new { settings = new Dictionary<string, string> { [SiteSettingKeys.Name] = "Nope" } },
                Token);
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        }

        using var anonymous = api.CreateClientWithoutCookieJar();
        using var unauthorised = await anonymous.GetAsync(new Uri("/api/v1/admin/settings", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorised.StatusCode);
    }

    [Fact]
    public async Task AnUnknownSetting_IsRefusedRatherThanStored()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);

        using var refused = await client.PutAsJsonAsync(
            "/api/v1/admin/settings",
            new { settings = new Dictionary<string, string> { ["site.invented.key"] = "x" } },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
        Assert.Equal("unknown_setting", problem!.Code);
    }

    [Fact]
    public async Task ALogo_IsUploaded_Served_AndPutBack()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);
        using var anonymous = api.CreateClientWithoutCookieJar();

        try
        {
            using var uploaded = await client.PutAsJsonAsync(
                "/api/v1/admin/settings/assets/logo",
                new { contentType = "image/png", base64 = OnePixelPng },
                Token);
            Assert.Equal(HttpStatusCode.NoContent, uploaded.StatusCode);

            var config = await anonymous.GetFromJsonAsync<Config>("/api/v1/site/config", TestData.Json, Token);
            var logo = config!.Assets.Single(asset => asset.Kind == "logo");
            Assert.True(logo.IsCustom);
            // Versioned, so a replacement is visible despite the long cache lifetime.
            Assert.StartsWith("/api/v1/site/assets/logo?v=", logo.Url, StringComparison.Ordinal);

            // Anyone can fetch it: it is the mark in the header.
            using var served = await anonymous.GetAsync(new Uri(logo.Url, UriKind.Relative), Token);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await served.Content.ReadAsByteArrayAsync(Token));
            Assert.Contains("max-age", served.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

            using var removed = await client.DeleteAsync(
                new Uri("/api/v1/admin/settings/assets/logo", UriKind.Relative), Token);
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

            // Removing it twice is a conflict, not a silent success.
            using var againRemoved = await client.DeleteAsync(
                new Uri("/api/v1/admin/settings/assets/logo", UriKind.Relative), Token);
            Assert.Equal(HttpStatusCode.Conflict, againRemoved.StatusCode);

            var after = await anonymous.GetFromJsonAsync<Config>("/api/v1/site/config", TestData.Json, Token);
            Assert.False(after!.Assets.Single(asset => asset.Kind == "logo").IsCustom);
        }
        finally
        {
            await ResetAssetsAsync(data);
        }
    }

    [Fact]
    public async Task SomethingThatIsNotAnImage_IsRefused()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);

        // An SVG, which is the dangerous one: it can carry script, and these files are served
        // to every visitor from the site's own origin.
        var svg = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"));

        using var refusedSvg = await client.PutAsJsonAsync(
            "/api/v1/admin/settings/assets/logo",
            new { contentType = "image/svg+xml", base64 = svg },
            Token);
        Assert.Equal(HttpStatusCode.BadRequest, refusedSvg.StatusCode);

        // PNG bytes that are not a PNG: the declared type is not taken on trust.
        using var refusedLie = await client.PutAsJsonAsync(
            "/api/v1/admin/settings/assets/logo",
            new { contentType = "image/png", base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not a png")) },
            Token);
        Assert.Equal(HttpStatusCode.BadRequest, refusedLie.StatusCode);

        using var refusedKind = await client.PutAsJsonAsync(
            "/api/v1/admin/settings/assets/invented",
            new { contentType = "image/png", base64 = OnePixelPng },
            Token);
        Assert.Equal(HttpStatusCode.NotFound, refusedKind.StatusCode);
    }

    // --- Helpers ---

    /// <summary>
    /// Clears every saved setting and its history. Settings are global, so a test that left one
    /// behind would change what every other test sees.
    /// </summary>
    private static async Task ResetAsync(TestData data)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM [Site].[SettingHistory];
            DELETE FROM [Site].[Setting];
            """,
            cancellationToken: Token));
    }

    private static async Task ResetAssetsAsync(TestData data)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [Site].[Asset];",
            cancellationToken: Token));
    }

    private static async Task<long> CreateRoleAsync(TestData data, string key, string[] permissions)
    {
        await using var connection = await data.OpenAsync();

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO [Main].[StaffRole] ([Key], [Name], [NameBn])
            OUTPUT inserted.[Id]
            VALUES (@Key, @Key, @Key);
            """,
            new { Key = key },
            cancellationToken: Token));

        foreach (var permission in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO [Main].[StaffRolePermission] ([StaffRoleId], [Permission]) VALUES (@Id, @Permission);",
                new { Id = id, Permission = permission },
                cancellationToken: Token));
        }

        return id;
    }

    private static async Task GrantAsync(TestData data, long userId, long roleId, long actorId)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO [Main].[UserStaffRole] ([UserId], [StaffRoleId], [GrantedById]) VALUES (@UserId, @RoleId, @ActorId);",
            new { UserId = userId, RoleId = roleId, ActorId = actorId },
            cancellationToken: Token));
    }

    private static async Task DeleteRoleAsync(TestData data, long roleId)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM [Main].[UserStaffRole] WHERE [StaffRoleId] = @Id;
            DELETE FROM [Main].[StaffRolePermission] WHERE [StaffRoleId] = @Id;
            DELETE FROM [Main].[StaffRole] WHERE [Id] = @Id;
            """,
            new { Id = roleId },
            cancellationToken: Token));
    }

    private static async Task<int> AuditCountAsync(TestData data, string action, long actorId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Safety].[AuditLog] WHERE [Action] = @Action AND [ActorId] = @ActorId;",
            new { Action = action, ActorId = actorId },
            cancellationToken: Token));
    }

    private sealed record Config(
        string Version,
        Identity Identity,
        Contact Contact,
        List<Social> Social,
        Theme Theme,
        List<Asset> Assets);

    private sealed record Identity(string Name, string NameBn, string Tagline, string Description);

    private sealed record Contact(string Email, string Phone, string Address);

    private sealed record Social(string Platform, string Url);

    private sealed record Theme(Dictionary<string, string> Colours, string BodyFont, string DisplayFont);

    private sealed record Asset(string Kind, string Url, bool IsCustom);

    private sealed record SettingsPage(List<string> EditableGroups, List<string> BodyFonts);

    private sealed record Saved(int Changed);

    private sealed record HistoryItem(string Key, string? OldValue, string? NewValue, string? ChangedByName);

    private sealed record Problem(string? Code, string? Detail);
}
