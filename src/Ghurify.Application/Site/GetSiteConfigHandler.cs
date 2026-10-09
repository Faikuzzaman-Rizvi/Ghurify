namespace Ghurify.Application.Site;

/// <summary>
/// The site's configuration, for anybody who opens it.
///
/// No permission check and no caller: this is the name on the header and the colour of the
/// buttons, which every visitor needs before they have signed in to anything.
///
/// What it serves is an explicit shape, named field by field in <see cref="SiteConfig"/>, rather
/// than whatever the settings table happens to hold. So a later release that keeps an SMTP
/// password or a gateway key in the same table cannot leak it through here by accident: it would
/// have to be added to this contract on purpose.
/// </summary>
public sealed class GetSiteConfigHandler(SiteConfigService config)
{
    public Task<SiteConfig> HandleAsync(CancellationToken cancellationToken) =>
        config.GetAsync(cancellationToken);
}
