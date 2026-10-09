using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ghurify.Domain.Site;
using Microsoft.Extensions.Caching.Memory;

namespace Ghurify.Application.Site;

/// <summary>
/// Reads the site's configuration and keeps it in memory.
///
/// Every page load wants this, so it must not be a database round trip each time. It is cached
/// for <see cref="CacheSeconds"/> and dropped immediately when this instance is the one that
/// changed it, so an admin sees their own change at once. On more than one API instance a change
/// takes up to that long to reach the others; when the platform runs more than one, this is the
/// thing to move to Redis, and nothing else about it changes.
///
/// Saved values are overlaid on <see cref="SiteSettingsCatalog"/>'s defaults, and keys the
/// catalogue does not define are dropped. So an untouched database renders the site the code
/// shipped with, and an older build never tries to honour a setting it does not understand.
/// </summary>
public sealed class SiteConfigService(ISiteSettingsRepository repository, IMemoryCache cache)
{
    /// <summary>
    /// Long enough that a busy site is not re-reading settings constantly, short enough that a
    /// change on another instance appears while the person who made it is still watching.
    /// </summary>
    private const int CacheSeconds = 30;

    private const string CacheKey = "site.config";

    /// <summary>Where the app's own images live when nothing has been uploaded over them.</summary>
    private static readonly Dictionary<string, string> BuiltInAssets = new(StringComparer.Ordinal)
    {
        [SiteAssetDefinition.Logo] = "/favicon.svg",
        [SiteAssetDefinition.LogoDark] = "/favicon.svg",
        [SiteAssetDefinition.Favicon] = "/favicon.svg",
        [SiteAssetDefinition.AppleTouchIcon] = "/apple-touch-icon.png",
        // The app ships no link-preview picture; an empty URL means "there is none", and the
        // web app leaves the meta tag out rather than pointing at a missing file.
        [SiteAssetDefinition.SocialImage] = "",
    };

    public async Task<SiteConfig> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out SiteConfig? cached) && cached is not null)
        {
            return cached;
        }

        var config = Build(await repository.QueryAsync(cancellationToken));

        cache.Set(CacheKey, config, TimeSpan.FromSeconds(CacheSeconds));
        return config;
    }

    /// <summary>
    /// Forgets the cached configuration after this instance changed it, so the next read is
    /// fresh rather than up to half a minute stale.
    /// </summary>
    public void Forget() => cache.Remove(CacheKey);

    /// <summary>
    /// The settings in force: the catalogue's defaults with the saved values over the top.
    /// Used by the panel (which shows both sides) and by the contrast check (which has to judge
    /// the whole theme, not just the colours being changed).
    /// </summary>
    public static Dictionary<string, string> Effective(IReadOnlyDictionary<string, string> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        var values = SiteSettingsCatalog.Defaults();

        foreach (var (key, value) in saved)
        {
            // A key this build does not define, or an empty value, falls back to the default.
            if (SiteSettingsCatalog.Exists(key) && !string.IsNullOrEmpty(value))
            {
                values[key] = value;
            }
        }

        return values;
    }

    internal static SiteConfig Build(StoredSiteSettings stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        var values = Effective(stored.Values);
        var assets = BuildAssets(stored.Assets);

        return new SiteConfig(
            VersionOf(stored),
            new SiteIdentityConfig(
                values[SiteSettingKeys.Name],
                values[SiteSettingKeys.NameBn],
                values[SiteSettingKeys.Tagline],
                values[SiteSettingKeys.TaglineBn],
                values[SiteSettingKeys.Description],
                values[SiteSettingKeys.DescriptionBn]),
            new SiteContactConfig(
                values[SiteSettingKeys.ContactEmail],
                values[SiteSettingKeys.ContactPhone],
                values[SiteSettingKeys.ContactAddress],
                values[SiteSettingKeys.ContactAddressBn]),
            BuildSocial(values),
            BuildTheme(values),
            assets);
    }

    private static IReadOnlyList<SiteSocialLink> BuildSocial(Dictionary<string, string> values) =>
        [.. SiteSettingsCatalog.InGroup(SettingGroup.Social)
            .Select(definition => new SiteSocialLink(
                // social.facebook -> facebook
                definition.Key[(definition.Key.IndexOf('.', StringComparison.Ordinal) + 1)..],
                values[definition.Key]))
            // A platform with no link is simply not there, so the footer renders no dead icons.
            .Where(link => link.Url.Length > 0)];

    private static SiteThemeConfig BuildTheme(Dictionary<string, string> values)
    {
        // Falling back on the default for a font that is no longer offered: a release may drop a
        // family, and a page with no font is worse than a page with the original one.
        var body = FontChoices.FindBody(values[SiteSettingKeys.FontBody]) ?? FontChoices.Body[0];
        var display = FontChoices.FindDisplay(values[SiteSettingKeys.FontDisplay]) ?? FontChoices.Display[0];

        var colours = SiteSettingsCatalog.InGroup(SettingGroup.Theme)
            .Where(definition => definition.Kind == SettingKind.Colour)
            .ToDictionary(
                // theme.colour.hill -> hill, which is the stylesheet's own token name.
                definition => definition.Key[(definition.Key.LastIndexOf('.') + 1)..],
                definition => values[definition.Key],
                StringComparer.Ordinal);

        return new SiteThemeConfig(
            colours,
            body.Family,
            display.Family,
            FontChoices.Stack(body.Family),
            FontChoices.Stack(display.Family),
            FontChoices.StylesheetUrl(body, display));
    }

    private static IReadOnlyList<SiteAssetLink> BuildAssets(IReadOnlyList<SiteAssetStamp> uploaded)
    {
        var stamps = uploaded.ToDictionary(stamp => stamp.Kind, StringComparer.Ordinal);

        return
        [
            .. SiteAssetDefinition.All.Select(definition =>
                stamps.TryGetValue(definition.Key, out var stamp)
                    // Versioned, so replacing a logo is visible at once despite the long cache
                    // lifetime the API serves it with.
                    ? new SiteAssetLink(
                        definition.Key,
                        $"/api/v1/site/assets/{definition.Key}?v={stamp.UpdatedOn.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}",
                        true)
                    : new SiteAssetLink(definition.Key, BuiltInAssets[definition.Key], false)),
        ];
    }

    /// <summary>
    /// A short stamp over everything that was stored. Equal inputs give an equal version, so the
    /// browser's cached copy stays valid across restarts and across instances.
    /// </summary>
    private static string VersionOf(StoredSiteSettings stored)
    {
        var builder = new StringBuilder();

        foreach (var (key, value) in stored.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        foreach (var asset in stored.Assets.OrderBy(asset => asset.Kind, StringComparer.Ordinal))
        {
            builder.Append(asset.Kind).Append('@').Append(asset.UpdatedOn.ToUnixTimeMilliseconds()).Append('\n');
        }

        // Not a secret, so a short hash is plenty; it only has to change when the input does.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }
}
