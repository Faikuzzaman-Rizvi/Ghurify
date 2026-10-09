namespace Ghurify.Domain.Site;

/// <summary>
/// The images a super admin can replace. A fixed list, because each one is rendered in a
/// particular place at a particular size; an open-ended gallery belongs to content, not branding.
/// </summary>
/// <param name="Key">Stored in <c>[Site].[Asset].[Kind]</c> and used in the asset's URL.</param>
/// <param name="MaxBytes">
/// The largest upload accepted, after the browser has already shrunk it. Generous for a logo and
/// mean compared with a photo: these are sent with every page, and they are held in the database
/// rather than in blob storage (see <c>Site.Asset</c>).
/// </param>
public sealed record SiteAssetDefinition(string Key, int MaxBytes, string Purpose)
{
    public const string Logo = "logo";
    public const string LogoDark = "logo-dark";
    public const string Favicon = "favicon";
    public const string AppleTouchIcon = "apple-touch-icon";
    public const string SocialImage = "social-image";

    /// <summary>512 KB: a logo that needs more than this is a photo in disguise.</summary>
    private const int Small = 512 * 1024;

    /// <summary>1 MB, for the link-preview image, which is a real photograph at 1200x630.</summary>
    private const int Large = 1024 * 1024;

    public static readonly IReadOnlyList<SiteAssetDefinition> All =
    [
        new(Logo, Small, "The mark in the header, on light backgrounds."),
        new(LogoDark, Small, "The mark on the footer and the admin sidebar."),
        new(Favicon, Small, "The browser tab icon."),
        new(AppleTouchIcon, Small, "The icon when the site is saved to a phone's home screen."),
        new(SocialImage, Large, "The picture shown when a link to the site is shared."),
    ];

    public static SiteAssetDefinition? Find(string? key) =>
        All.FirstOrDefault(asset => string.Equals(asset.Key, key, StringComparison.Ordinal));

    public static bool Exists(string? key) => Find(key) is not null;
}
