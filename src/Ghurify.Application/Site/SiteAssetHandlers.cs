using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Site;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Site;

/// <summary>
/// Replaces one of the site's images: the logo, the two icons, or the link-preview picture.
///
/// The bytes are checked to be a real image of the type claimed and stripped of their metadata
/// before anything keeps them — the same check every profile picture goes through, because an
/// SVG or a doctored PNG served from the site's own origin would run in every visitor's browser.
/// SVG is not accepted at all for this reason: it can carry script, and these files are served
/// to everyone.
/// </summary>
public sealed class UploadSiteAssetHandler(
    ISiteSettingsRepository settings,
    SiteConfigService config,
    AccessService access,
    IAuditLog audit,
    ILogger<UploadSiteAssetHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(
        long actorId,
        string kind,
        UploadSiteAssetCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.SettingsBranding))
        {
            return AppError.Forbidden();
        }

        if (SiteAssetDefinition.Find(kind) is not { } definition)
        {
            return AppError.NotFound("unknown_asset", "There is no site image of that kind.");
        }

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(command.Base64);
        }
        catch (FormatException)
        {
            return AppError.Validation("upload_not_image", "The file did not arrive in one piece. Try again.");
        }

        if (raw.Length == 0)
        {
            return AppError.Validation("upload_not_image", "That file is empty.");
        }

        if (raw.Length > definition.MaxBytes)
        {
            return AppError.Validation(
                "upload_too_large",
                $"Keep this image under {definition.MaxBytes / 1024} KB.");
        }

        // Checks the bytes really are the declared type, and returns them without the metadata a
        // phone or a design tool leaves behind.
        var image = UploadedImage.Check(raw, command.ContentType);
        if (!image.Succeeded)
        {
            return AppError.Validation(image.ErrorCode!, image.Error!);
        }

        var contentType = UploadedImage.ContentTypeOf(image.Format);

        await settings.SaveAssetAsync(kind, contentType, image.Bytes!, actorId, cancellationToken);
        config.Forget();

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                "site.asset.update",
                "SiteAsset",
                EntityId: 0,
                Note: kind,
                Changes: new AuditChanges()
                    .Set("image", "(previous)", $"{contentType}, {image.Bytes!.Length / 1024} KB")
                    .ToJson()),
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} replaced the site {Kind} ({ContentType}, {Bytes} bytes).",
            actorId, kind, contentType, image.Bytes!.Length);

        return Done.Value;
    }
}

/// <summary>
/// Puts one of the site's images back to the file the app shipped with. Nothing is deleted: the
/// uploaded bytes stay archived, so a change made by mistake can still be recovered from the
/// database.
/// </summary>
public sealed class RemoveSiteAssetHandler(
    ISiteSettingsRepository settings,
    SiteConfigService config,
    AccessService access,
    IAuditLog audit,
    ILogger<RemoveSiteAssetHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, string kind, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.SettingsBranding))
        {
            return AppError.Forbidden();
        }

        if (!SiteAssetDefinition.Exists(kind))
        {
            return AppError.NotFound("unknown_asset", "There is no site image of that kind.");
        }

        if (!await settings.RemoveAssetAsync(kind, actorId, cancellationToken))
        {
            return AppError.Conflict("asset_already_default", "That image is already the built-in one.");
        }

        config.Forget();

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                "site.asset.reset",
                "SiteAsset",
                EntityId: 0,
                Note: kind,
                Changes: new AuditChanges().Set("image", "(uploaded)", "(built in)").ToJson()),
            cancellationToken);

        logger.LogInformation("Admin {ActorId} put the site {Kind} back to the built-in image.", actorId, kind);

        return Done.Value;
    }
}

/// <summary>
/// Serves one of the site's images. Anonymous, like the configuration that points at it: a logo
/// is on display by definition, and the header would not render without it.
/// </summary>
public sealed class GetSiteAssetHandler(ISiteSettingsRepository settings)
{
    public async Task<SiteAsset?> HandleAsync(string kind, CancellationToken cancellationToken) =>
        SiteAssetDefinition.Exists(kind)
            ? await settings.GetAssetAsync(kind, cancellationToken)
            : null;
}

/// <summary>What one setting used to be, for the panel's history popover.</summary>
public sealed class GetSettingHistoryHandler(ISiteSettingsRepository settings, AccessService access)
{
    public async Task<Result<IReadOnlyList<SettingChangeRecord>>> HandleAsync(
        long actorId,
        string key,
        CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);

        if (SiteSettingsCatalog.Find(key) is not { } definition)
        {
            return AppError.NotFound("unknown_setting", "There is no setting with that name.");
        }

        // Seeing what a setting used to be is part of being able to work with it.
        if (!GetSettingsHandler.EditableGroups(actor).Contains(definition.Group))
        {
            return AppError.Forbidden();
        }

        return Result.Ok(await settings.QueryHistoryAsync(key, take: 20, cancellationToken));
    }
}
