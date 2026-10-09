using System.Globalization;
using Ghurify.Domain.Identity;

namespace Ghurify.Domain.Site;

/// <summary>Why one setting was refused. <see cref="Key"/> lets the panel mark the right field.</summary>
public sealed record SettingProblem(string Key, string Code, string Message);

/// <summary>
/// Checks a proposed change to the site's settings.
///
/// Two kinds of rule. Per value: is this really a colour, a URL on the right host, a font we can
/// load? And across values: can the text colours still be read against the backgrounds they are
/// used on? The second kind is the one that matters most, because a theme that passes every
/// individual check can still leave the site unusable, and the person who did it may be the only
/// one who can undo it.
/// </summary>
public static class SiteSettingRules
{
    /// <summary>WCAG 2.1 AA for ordinary text.</summary>
    public const double MinimumTextContrast = 4.5;

    /// <summary>WCAG 2.1 AA for large text and the edges of controls.</summary>
    public const double MinimumLargeTextContrast = 3.0;

    /// <summary>
    /// Checks one value for one key. Returns null when it is fine.
    ///
    /// An empty value means "use the default" for a required setting and "leave it out" for an
    /// optional one, so emptiness is never itself an error.
    /// </summary>
    public static SettingProblem? CheckValue(SettingDefinition definition, string value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            return null;
        }

        if (value.Length > definition.MaxLength)
        {
            return new SettingProblem(
                definition.Key,
                "setting_too_long",
                $"Keep this to {definition.MaxLength.ToString(CultureInfo.InvariantCulture)} characters.");
        }

        // A control character would travel into the page, an email or a stylesheet. No setting
        // has a use for one.
        if (value.Any(char.IsControl))
        {
            return new SettingProblem(definition.Key, "setting_invalid", "Remove the line breaks and control characters.");
        }

        return definition.Kind switch
        {
            SettingKind.Colour when !BrandColour.TryParse(value, out _) =>
                new SettingProblem(definition.Key, "setting_not_colour", "Write a colour as #rrggbb, for example #245c43."),

            SettingKind.EmailAddress when !EmailAddress.TryParse(value, out _) =>
                new SettingProblem(definition.Key, "setting_not_email", "That is not an email address."),

            SettingKind.Phone when !PhoneNumber.TryParse(value, out _) =>
                new SettingProblem(definition.Key, "setting_not_phone", "Write a Bangladeshi number, for example +8801712345678."),

            SettingKind.Url => CheckUrl(definition, value),

            SettingKind.Font => CheckFont(definition, value),

            _ => null,
        };
    }

    private static SettingProblem? CheckUrl(SettingDefinition definition, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url)
            || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return new SettingProblem(definition.Key, "setting_not_url", "Write the full address, starting with https://.");
        }

        // Credentials in a URL would be shown to every visitor, and are a sign of a pasted mistake.
        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            return new SettingProblem(definition.Key, "setting_not_url", "Remove the username and password from the address.");
        }

        if (definition.UrlHost is { } expected && !IsOn(url.Host, expected))
        {
            return new SettingProblem(
                definition.Key,
                "setting_wrong_host",
                $"This link has to be on {expected}.");
        }

        return null;
    }

    /// <summary>
    /// Whether a host is the expected domain or one of its subdomains, compared label by label
    /// so <c>facebook.com.example.net</c> and <c>notfacebook.com</c> both fail.
    /// </summary>
    private static bool IsOn(string host, string domain) =>
        string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    private static SettingProblem? CheckFont(SettingDefinition definition, string value)
    {
        var known = definition.Key == SiteSettingKeys.FontDisplay
            ? FontChoices.FindDisplay(value)
            : FontChoices.FindBody(value);

        return known is null
            ? new SettingProblem(definition.Key, "setting_unknown_font", "Choose one of the offered fonts.")
            : null;
    }

    /// <summary>
    /// Checks the colours against each other, in the combinations the site actually paints.
    ///
    /// <paramref name="colours"/> is the whole theme after the change — saved values over
    /// defaults — because a pair can fail on a colour the caller did not touch this time.
    /// </summary>
    public static IReadOnlyList<SettingProblem> CheckContrast(IReadOnlyDictionary<string, string> colours)
    {
        ArgumentNullException.ThrowIfNull(colours);

        var problems = new List<SettingProblem>();
        var white = BrandColour.White;

        // Where each colour has to stay readable. The keys name the setting the panel should
        // mark; the comment on each says where in the site that combination appears.
        var pairs = new (string Key, string? AgainstKey, double Minimum, string Where)[]
        {
            // Links, buttons and labels sit on white and on mist.
            (SiteSettingKeys.ColourHill, null, MinimumTextContrast, "text on white"),
            (SiteSettingKeys.ColourHill, SiteSettingKeys.ColourMist, MinimumTextContrast, "text on the tinted sections"),

            // Headings and body text.
            (SiteSettingKeys.ColourDeep, null, MinimumTextContrast, "headings on white"),
            (SiteSettingKeys.ColourDeep, SiteSettingKeys.ColourMist, MinimumTextContrast, "headings on the tinted sections"),

            // The footer and the safety band carry white text.
            (SiteSettingKeys.ColourNight, null, MinimumTextContrast, "white text on the footer"),

            // The accent at text weight: the one that is used for words on white.
            (SiteSettingKeys.ColourOchre, null, MinimumTextContrast, "accent text on white"),

            // Errors and warnings.
            (SiteSettingKeys.ColourJamdani, null, MinimumTextContrast, "warnings on white"),

            // Turmeric is a fill, never a word on white, so it only has to hold the dark text
            // that sits on it — a badge, the active sidebar icon.
            (SiteSettingKeys.ColourTurmeric, SiteSettingKeys.ColourNight, MinimumLargeTextContrast, "dark text on the accent fill"),

            // Mist is a background: white has to be distinguishable from it, or a card loses its
            // edge against the section behind it.
            (SiteSettingKeys.ColourMist, null, 1.05, "the tinted sections against white cards"),
        };

        foreach (var (key, againstKey, minimum, where) in pairs)
        {
            if (!TryColour(colours, key, out var colour))
            {
                // Not a colour at all; CheckValue already reported that, and reporting it twice
                // would only clutter the panel.
                continue;
            }

            var against = white;
            if (againstKey is not null && !TryColour(colours, againstKey, out against))
            {
                continue;
            }

            var ratio = BrandColour.Contrast(colour, against);
            if (ratio < minimum)
            {
                problems.Add(new SettingProblem(
                    key,
                    "setting_low_contrast",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Too faint for {where}: {ratio:0.0} to 1, and {minimum:0.0} is the least that can be read.")));
            }
        }

        return problems;
    }

    private static bool TryColour(
        IReadOnlyDictionary<string, string> colours,
        string key,
        out BrandColour colour)
    {
        colour = default;
        return colours.TryGetValue(key, out var text) && BrandColour.TryParse(text, out colour);
    }
}
