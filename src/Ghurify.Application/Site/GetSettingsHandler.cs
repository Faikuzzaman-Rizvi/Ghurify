using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Site;

namespace Ghurify.Application.Site;

/// <summary>
/// The settings screens: every setting, what it is set to, what it would go back to, and the
/// state of each image.
///
/// Opens for anybody who may edit at least one group, and says which groups those are. The panel
/// then shows the rest read-only rather than hiding it: knowing the site's colours without being
/// able to change them is useful, and pretending the section does not exist is not.
/// </summary>
public sealed class GetSettingsHandler(
    ISiteSettingsRepository settings,
    SiteConfigService config,
    AccessService access)
{
    public async Task<Result<SettingsView>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        var editable = EditableGroups(actor);

        if (editable.Count == 0)
        {
            return AppError.Forbidden();
        }

        var stored = await settings.QueryAsync(cancellationToken);
        var effective = SiteConfigService.Effective(stored.Values);
        var siteConfig = await config.GetAsync(cancellationToken);
        var uploaded = stored.Assets.ToDictionary(asset => asset.Kind, StringComparer.Ordinal);

        return new SettingsView(
            [.. SiteSettingsCatalog.All.Select(definition => new SettingView(
                definition.Key,
                definition.Group,
                definition.Kind,
                effective[definition.Key],
                definition.Default,
                // "Changed from the default" is what the panel marks, and what reset undoes.
                stored.Values.ContainsKey(definition.Key),
                definition.Required,
                definition.MaxLength))],
            editable,
            [.. FontChoices.Body.Select(choice => choice.Family)],
            [.. FontChoices.Display.Select(choice => choice.Family)],
            [.. SiteAssetDefinition.All.Select(definition =>
            {
                var link = siteConfig.Assets.First(asset => asset.Kind == definition.Key);
                var stamp = uploaded.GetValueOrDefault(definition.Key);

                return new SiteAssetView(
                    definition.Key,
                    link.Url,
                    link.IsCustom,
                    definition.MaxBytes,
                    stamp?.SizeBytes,
                    stamp?.UpdatedOn);
            })]);
    }

    /// <summary>
    /// The groups this caller may change. Branding covers the words and the pictures; theme
    /// covers the colours and type, which is a separate permission because a bad theme can make
    /// every page unreadable, including the one used to put it right.
    /// </summary>
    internal static IReadOnlyList<SettingGroup> EditableGroups(UserAccess actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var groups = new List<SettingGroup>();

        if (actor.Can(Permissions.SettingsBranding))
        {
            groups.Add(SettingGroup.Identity);
            groups.Add(SettingGroup.Contact);
            groups.Add(SettingGroup.Social);
        }

        if (actor.Can(Permissions.SettingsTheme))
        {
            groups.Add(SettingGroup.Theme);
        }

        return groups;
    }
}
