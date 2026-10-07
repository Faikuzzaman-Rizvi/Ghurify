using Ghurify.Application.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Social;

/// <summary>
/// Turns the blob names the repository returns into short-lived read links. The container is
/// private: a link stops working after a while, so a scraped feed cannot become a permanent mirror.
/// </summary>
public sealed class MediaLinks(IMediaStorage storage, IClock clock, IOptions<MediaOptions> options)
{
    public PostPage Sign(PostPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!storage.IsConfigured)
        {
            return page with { Items = [.. page.Items.Select(post => post with { Media = [] })] };
        }

        var expires = clock.UtcNow.AddMinutes(options.Value.ReadLinkMinutes);

        return page with
        {
            Items = [.. page.Items.Select(post => post with
            {
                Media = [.. post.Media.Select(media => media with { Url = storage.CreateReadUrl(media.Url, expires).ToString() })],
            })],
        };
    }
}
