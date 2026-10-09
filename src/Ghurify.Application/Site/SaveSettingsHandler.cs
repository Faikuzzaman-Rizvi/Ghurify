using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Site;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Site;

/// <summary>
/// Saves a batch of site settings.
///
/// Everything is checked before anything is written, and then written in one transaction, so a
/// theme is never half-applied. Three gates, in this order:
///   * the caller may edit the group each key belongs to;
///   * each value is the right shape for its key (a colour is a colour, a Facebook link is on
///     facebook.com, a font is one we can load);
///   * the colours can still be read against the backgrounds they are painted on — judged over
///     the whole theme after the change, not just the keys in this request, because a pair can
///     fail on a colour the caller did not touch.
///
/// The last gate is the one that earns its place. Everything else here is a typo; a theme that
/// passes every individual check can still leave the site unusable, and the person who did it
/// may be the only one who could put it back.
/// </summary>
public sealed class SaveSettingsHandler(
    ISiteSettingsRepository settings,
    SiteConfigService config,
    AccessService access,
    IAuditLog audit,
    ILogger<SaveSettingsHandler> logger)
{
    public async Task<Result<SettingsSaved>> HandleAsync(
        long actorId,
        SaveSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = await access.GetAsync(actorId, cancellationToken);
        var editable = GetSettingsHandler.EditableGroups(actor);

        if (editable.Count == 0)
        {
            return AppError.Forbidden();
        }

        if (command.Settings.Count == 0)
        {
            return AppError.Validation("no_settings", "There is nothing to save.");
        }

        // Guards against a request with thousands of keys; the panel sends at most a screenful.
        if (command.Settings.Count > SiteSettingsCatalog.All.Count)
        {
            return AppError.Validation("too_many_settings", "That is more settings than exist.");
        }

        var problems = new List<SettingProblem>();
        var wanted = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, raw) in command.Settings)
        {
            if (SiteSettingsCatalog.Find(key) is not { } definition)
            {
                return AppError.Validation("unknown_setting", $"There is no setting called \"{key}\".");
            }

            if (!editable.Contains(definition.Group))
            {
                return AppError.Forbidden($"You cannot change the {definition.Group} settings.");
            }

            var value = (raw ?? string.Empty).Trim();

            // A required setting cannot be blanked: empty means "back to the default", which is
            // always something, so the site never renders with no name and no colours.
            if (SiteSettingRules.CheckValue(definition, value) is { } problem)
            {
                problems.Add(problem);
                continue;
            }

            wanted[key] = value;
        }

        if (problems.Count > 0)
        {
            return Refused(problems);
        }

        // The whole theme as it would be, so a pair that fails on an untouched colour is caught.
        var stored = await settings.QueryAsync(cancellationToken);
        var after = SiteConfigService.Effective(stored.Values);
        foreach (var (key, value) in wanted)
        {
            after[key] = value.Length == 0 ? SiteSettingsCatalog.Find(key)!.Default : value;
        }

        var contrast = SiteSettingRules.CheckContrast(after);
        if (contrast.Count > 0)
        {
            return Refused(contrast);
        }

        var changes = await settings.SaveAsync(wanted, actorId, cancellationToken);

        if (changes.Count == 0)
        {
            // Nothing moved, so nothing is audited: saving a form untouched should leave no trace.
            return new SettingsSaved(0);
        }

        config.Forget();

        var record = new AuditChanges();
        foreach (var change in changes.OrderBy(change => change.Key, StringComparer.Ordinal))
        {
            // Null on either side reads as the default, which is what it means.
            record.Set(change.Key, change.OldValue ?? "(default)", change.NewValue ?? "(default)");
        }

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                "site.settings.update",
                "SiteSetting",
                // Settings are keyed by name, not by id; the keys that moved are in the changes.
                EntityId: 0,
                Note: string.Join(", ", changes.Select(change => change.Key).Order(StringComparer.Ordinal)),
                Changes: record.ToJson()),
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} changed {Count} site setting(s): {Keys}.",
            actorId,
            changes.Count,
            string.Join(", ", changes.Select(change => change.Key)));

        return new SettingsSaved(changes.Count);
    }

    private static AppError Refused(IReadOnlyList<SettingProblem> problems)
    {
        // The first problem decides the code, so the panel can mark that field; the message
        // carries them all, because a theme usually fails in more than one place at once.
        var first = problems[0];

        return AppError.Validation(
            first.Code,
            string.Join(" ", problems.Select(problem => $"{problem.Key}: {problem.Message}")));
    }
}

/// <param name="Changed">How many settings actually moved. Zero when the form was unchanged.</param>
public sealed record SettingsSaved(int Changed);
