using Ghurify.Domain.Site;

namespace Ghurify.Application.Site;

/// <summary>
/// The site's own configuration: what a super admin has changed about its name, branding and
/// theme, and the images they have uploaded.
/// </summary>
public interface ISiteSettingsRepository
{
    /// <summary>
    /// Everything that differs from the shipped defaults, plus a stamp for each uploaded image.
    /// One round trip, because this is read to build the whole configuration at once.
    /// </summary>
    Task<StoredSiteSettings> QueryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves a batch in one transaction and reports what actually moved. An empty value puts a
    /// setting back on its default. Values equal to what is stored change nothing.
    /// </summary>
    Task<IReadOnlyList<SettingChange>> SaveAsync(
        IReadOnlyDictionary<string, string> settings,
        long actorId,
        CancellationToken cancellationToken);

    /// <summary>What one setting used to be, newest first.</summary>
    Task<IReadOnlyList<SettingChangeRecord>> QueryHistoryAsync(
        string key,
        int take,
        CancellationToken cancellationToken);

    /// <summary>One uploaded image's bytes, or null when that kind is on the built-in file.</summary>
    Task<SiteAsset?> GetAssetAsync(string kind, CancellationToken cancellationToken);

    /// <summary>Replaces an image, archiving the one before it.</summary>
    Task SaveAssetAsync(
        string kind,
        string contentType,
        byte[] bytes,
        long actorId,
        CancellationToken cancellationToken);

    /// <summary>Puts an image back to the built-in one. False when it already was.</summary>
    Task<bool> RemoveAssetAsync(string kind, long actorId, CancellationToken cancellationToken);
}

/// <summary>What the database holds: only the settings that differ, and the live images.</summary>
public sealed record StoredSiteSettings(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<SiteAssetStamp> Assets);

/// <summary>
/// An uploaded image without its bytes: enough to build its URL and know when it last changed.
/// </summary>
/// <param name="UpdatedOn">
/// Cache-busts the asset's URL and becomes its ETag, so a replaced logo appears at once and an
/// unchanged one is never sent twice.
/// </param>
public sealed record SiteAssetStamp(string Kind, string ContentType, int SizeBytes, DateTimeOffset UpdatedOn);

/// <summary>An uploaded image, ready to serve.</summary>
public sealed record SiteAsset(string Kind, string ContentType, byte[] Bytes, DateTimeOffset UpdatedOn);

/// <summary>One setting that moved. Null on either side means "was, or is now, on its default".</summary>
public sealed record SettingChange(string Key, string? OldValue, string? NewValue);

/// <summary>A change as the panel's history list shows it.</summary>
public sealed record SettingChangeRecord(
    long Id,
    string Key,
    string? OldValue,
    string? NewValue,
    long ChangedById,
    string? ChangedByName,
    DateTimeOffset Created);
