using Ghurify.Domain.Site;

namespace Ghurify.Application.Site;

/// <summary>
/// Everything the web app needs to render itself as this particular site: what it is called,
/// how to reach it, what colour it is and which images to use.
///
/// Served to everyone, signed in or not, from one anonymous endpoint, because the header and the
/// footer are the first things painted. Nothing in here is private: it is all on display by
/// definition.
/// </summary>
/// <param name="Version">
/// Changes whenever any of this does. The browser caches against it, and the web app can tell a
/// configuration change from a reload.
/// </param>
public sealed record SiteConfig(
    string Version,
    SiteIdentityConfig Identity,
    SiteContactConfig Contact,
    IReadOnlyList<SiteSocialLink> Social,
    SiteThemeConfig Theme,
    IReadOnlyList<SiteAssetLink> Assets);

/// <param name="Name">Both languages, because the header is rendered in whichever is chosen.</param>
public sealed record SiteIdentityConfig(
    string Name,
    string NameBn,
    string Tagline,
    string TaglineBn,
    string Description,
    string DescriptionBn);

/// <summary>Empty strings where nothing is set: the footer then leaves the line out.</summary>
public sealed record SiteContactConfig(
    string Email,
    string Phone,
    string Address,
    string AddressBn);

/// <param name="Platform">The key from <see cref="SiteSettingKeys"/>, minus the group, e.g. <c>facebook</c>.</param>
public sealed record SiteSocialLink(string Platform, string Url);

/// <param name="Colours">
/// Keyed by the token name the stylesheet uses (<c>hill</c>, <c>deep</c>, <c>turmeric</c>...), so
/// the web app can set each one as a CSS variable without knowing what any of them are for.
/// </param>
/// <param name="FontStylesheet">
/// The one Google Fonts stylesheet both families need, built from the allowed list. The web app
/// adds it to the page; it never constructs a third-party URL itself.
/// </param>
public sealed record SiteThemeConfig(
    IReadOnlyDictionary<string, string> Colours,
    string BodyFont,
    string DisplayFont,
    string BodyFontStack,
    string DisplayFontStack,
    string FontStylesheet);

/// <param name="Url">
/// Where to fetch it. An uploaded image gets a versioned URL on the API; one that has never been
/// replaced gets the path of the file the app shipped with, so the browser takes it from the
/// static host rather than through the API.
/// </param>
/// <param name="IsCustom">Whether a super admin replaced this one.</param>
public sealed record SiteAssetLink(string Kind, string Url, bool IsCustom);

// --- The panel's own view of the same thing ---

/// <summary>
/// One setting as the panel edits it: what it is, what it is set to, and what it would go back
/// to. Sent with the value and the default side by side so "reset" needs no second call.
/// </summary>
/// <param name="Value">
/// What is in force. Equal to <paramref name="Default"/> when nothing has been saved.
/// </param>
/// <param name="IsCustom">Whether this has been changed from the default.</param>
public sealed record SettingView(
    string Key,
    SettingGroup Group,
    SettingKind Kind,
    string Value,
    string Default,
    bool IsCustom,
    bool Required,
    int MaxLength);

/// <summary>
/// The settings screens: every setting the caller may edit, the font families on offer, and the
/// state of each image.
/// </summary>
/// <param name="EditableGroups">
/// Which groups this caller may change. Branding and theme are separate permissions, so somebody
/// may be able to rename the site without being able to recolour it.
/// </param>
public sealed record SettingsView(
    IReadOnlyList<SettingView> Settings,
    IReadOnlyList<SettingGroup> EditableGroups,
    IReadOnlyList<string> BodyFonts,
    IReadOnlyList<string> DisplayFonts,
    IReadOnlyList<SiteAssetView> Assets);

public sealed record SiteAssetView(
    string Kind,
    string Url,
    bool IsCustom,
    int MaxBytes,
    int? SizeBytes,
    DateTimeOffset? UpdatedOn);

/// <summary>A batch of changes. Keys the caller may not edit, or does not know, are refused.</summary>
public sealed record SaveSettingsCommand(IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// An uploaded image, as base64.
/// </summary>
/// <remarks>
/// Base64 in the JSON body rather than a multipart form or a storage link, because these are a
/// handful of images of at most a megabyte, the browser has already shrunk them, and the bytes
/// end up in the database rather than in blob storage. One request, the same shape as every
/// other call, and no upload ticket to expire.
/// </remarks>
public sealed record UploadSiteAssetCommand(string ContentType, string Base64);
