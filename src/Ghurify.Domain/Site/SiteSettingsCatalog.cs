namespace Ghurify.Domain.Site;

/// <summary>
/// One setting, as the panel renders it and the API checks it.
/// </summary>
/// <param name="Default">
/// What the site uses when nothing has been saved. These are the values that used to be
/// hard-coded in the app, so an untouched database renders exactly the Ghurify the code shipped
/// with, and "reset" always has somewhere to go back to.
/// </param>
/// <param name="MaxLength">
/// The longest value accepted. Chosen per setting from where it appears: a site name has to fit
/// a phone header, a description has to fit a link preview.
/// </param>
/// <param name="Required">
/// Whether the site needs a value. A required setting falls back to its default rather than
/// going blank; an optional one (a social link) is simply absent when empty.
/// </param>
/// <param name="UrlHost">
/// For a <see cref="SettingKind.Url"/>, the host the link must be on. A "Facebook" link that
/// points anywhere else is somebody using the site's own footer to send its visitors elsewhere.
/// </param>
public sealed record SettingDefinition(
    string Key,
    SettingGroup Group,
    SettingKind Kind,
    string Default,
    bool Required = false,
    int MaxLength = 200,
    string? UrlHost = null);

/// <summary>
/// Every setting the code knows about: its default, its shape and how long it may be. The one
/// source of truth — the panel renders this, and nothing outside it can be stored.
/// </summary>
public static class SiteSettingsCatalog
{
    /// <summary>In panel order, grouped.</summary>
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        // --- Who the site is. The defaults are what the app shipped with. ---
        new(SiteSettingKeys.Name, SettingGroup.Identity, SettingKind.Text, "Ghurify", Required: true, MaxLength: 40),
        new(SiteSettingKeys.NameBn, SettingGroup.Identity, SettingKind.Text, "ঘুরিফাই", Required: true, MaxLength: 40),
        new(SiteSettingKeys.Tagline, SettingGroup.Identity, SettingKind.Text, "Travel together, safely", MaxLength: 80),
        new(SiteSettingKeys.TaglineBn, SettingGroup.Identity, SettingKind.Text, "একসাথে ঘুরুন, নিরাপদে", MaxLength: 80),
        new(
            SiteSettingKeys.Description,
            SettingGroup.Identity,
            SettingKind.LongText,
            "Ghurify: find a trip, join a group and travel safely across Bangladesh.",
            MaxLength: 200),
        new(
            SiteSettingKeys.DescriptionBn,
            SettingGroup.Identity,
            SettingKind.LongText,
            "ঘুরিফাই: ট্রিপ খুঁজুন, দলে যোগ দিন আর সারা বাংলাদেশ ঘুরুন নিরাপদে।",
            MaxLength: 200),

        // --- How to reach the company. Empty by default: better absent than invented. ---
        new(SiteSettingKeys.ContactEmail, SettingGroup.Contact, SettingKind.EmailAddress, "", MaxLength: 256),
        new(SiteSettingKeys.ContactPhone, SettingGroup.Contact, SettingKind.Phone, "", MaxLength: 20),
        new(SiteSettingKeys.ContactAddress, SettingGroup.Contact, SettingKind.LongText, "", MaxLength: 200),
        new(SiteSettingKeys.ContactAddressBn, SettingGroup.Contact, SettingKind.LongText, "", MaxLength: 200),

        // --- Where the company is online. Each link is pinned to its own platform's host. ---
        new(SiteSettingKeys.SocialFacebook, SettingGroup.Social, SettingKind.Url, "", MaxLength: 200, UrlHost: "facebook.com"),
        new(SiteSettingKeys.SocialInstagram, SettingGroup.Social, SettingKind.Url, "", MaxLength: 200, UrlHost: "instagram.com"),
        new(SiteSettingKeys.SocialYouTube, SettingGroup.Social, SettingKind.Url, "", MaxLength: 200, UrlHost: "youtube.com"),
        new(SiteSettingKeys.SocialX, SettingGroup.Social, SettingKind.Url, "", MaxLength: 200, UrlHost: "x.com"),
        new(SiteSettingKeys.SocialLinkedIn, SettingGroup.Social, SettingKind.Url, "", MaxLength: 200, UrlHost: "linkedin.com"),
        new(SiteSettingKeys.SocialTikTok, SettingGroup.Social, SettingKind.Url, "", MaxLength: 200, UrlHost: "tiktok.com"),

        // --- Colours. The defaults are the tokens from src/styles/index.css. ---
        new(SiteSettingKeys.ColourHill, SettingGroup.Theme, SettingKind.Colour, "#245c43", Required: true, MaxLength: 7),
        new(SiteSettingKeys.ColourDeep, SettingGroup.Theme, SettingKind.Colour, "#173f2e", Required: true, MaxLength: 7),
        new(SiteSettingKeys.ColourNight, SettingGroup.Theme, SettingKind.Colour, "#0f2a1f", Required: true, MaxLength: 7),
        new(SiteSettingKeys.ColourTurmeric, SettingGroup.Theme, SettingKind.Colour, "#d99a12", Required: true, MaxLength: 7),
        new(SiteSettingKeys.ColourOchre, SettingGroup.Theme, SettingKind.Colour, "#9a6500", Required: true, MaxLength: 7),
        new(SiteSettingKeys.ColourJamdani, SettingGroup.Theme, SettingKind.Colour, "#a3305c", Required: true, MaxLength: 7),
        new(SiteSettingKeys.ColourMist, SettingGroup.Theme, SettingKind.Colour, "#f1f5f2", Required: true, MaxLength: 7),

        // --- Type ---
        new(SiteSettingKeys.FontBody, SettingGroup.Theme, SettingKind.Font, "Barlow", Required: true, MaxLength: 40),
        new(SiteSettingKeys.FontDisplay, SettingGroup.Theme, SettingKind.Font, "Poppins", Required: true, MaxLength: 40),
    ];

    private static readonly Dictionary<string, SettingDefinition> ByKey =
        All.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    public static bool Exists(string key) => ByKey.ContainsKey(key);

    public static SettingDefinition? Find(string key) =>
        ByKey.TryGetValue(key, out var definition) ? definition : null;

    public static IEnumerable<SettingDefinition> InGroup(SettingGroup group) =>
        All.Where(definition => definition.Group == group);

    /// <summary>
    /// Every default, so an untouched database still renders the site. Callers overlay the saved
    /// values on top of this, which is also what makes a rollback safe: a key this build does not
    /// know simply is not in here, and the saved value for it is never read.
    /// </summary>
    public static Dictionary<string, string> Defaults() =>
        All.ToDictionary(definition => definition.Key, definition => definition.Default, StringComparer.Ordinal);
}
