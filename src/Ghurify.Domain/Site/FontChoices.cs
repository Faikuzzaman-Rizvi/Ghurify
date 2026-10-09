namespace Ghurify.Domain.Site;

/// <summary>
/// The font families a super admin may choose between.
///
/// A fixed list, not free text, for three reasons: the family has to exist on Google Fonts for
/// the stylesheet to resolve, the content-security policy only allows that one host, and a free
/// text field here would be a way to put an arbitrary URL into every page of the site.
///
/// Only the Latin family is chosen. The Bangla fallbacks are appended to every stack by
/// <see cref="Stack"/> and cannot be removed: Bangla has to render whatever anybody picks, so it
/// is a correctness rule rather than a style choice. Each family below carries every weight the
/// app asks for, so a change can never silently fall back to a synthesised bold.
/// </summary>
public sealed record FontChoice(string Family, string Weights, bool CoversBangla)
{
    /// <summary>The <c>family=</c> fragment for the Google Fonts stylesheet.</summary>
    public string Spec => $"{Family.Replace(' ', '+')}:wght@{Weights}";
}

public static class FontChoices
{
    /// <summary>Bangla glyphs fall through to these, so mixed text never drops to a system font.</summary>
    private const string BanglaFallback = "'Hind Siliguri', 'Noto Sans Bengali'";

    private const string SystemFallback =
        "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif";

    /// <summary>Weights the body text uses.</summary>
    private const string BodyWeights = "400;500;600";

    /// <summary>Weights the headings use.</summary>
    private const string DisplayWeights = "500;600;700";

    public static readonly IReadOnlyList<FontChoice> Body =
    [
        new("Barlow", BodyWeights, false),
        new("Inter", BodyWeights, false),
        new("Source Sans 3", BodyWeights, false),
        new("Noto Sans", BodyWeights, false),
        new("Hind Siliguri", BodyWeights, true),
    ];

    public static readonly IReadOnlyList<FontChoice> Display =
    [
        new("Poppins", DisplayWeights, false),
        new("Baloo Da 2", DisplayWeights, true),
        new("Playfair Display", DisplayWeights, false),
        new("Montserrat", DisplayWeights, false),
        new("Hind Siliguri", DisplayWeights, true),
    ];

    public static FontChoice? FindBody(string? family) =>
        Body.FirstOrDefault(choice => string.Equals(choice.Family, family, StringComparison.Ordinal));

    public static FontChoice? FindDisplay(string? family) =>
        Display.FirstOrDefault(choice => string.Equals(choice.Family, family, StringComparison.Ordinal));

    /// <summary>
    /// The CSS font stack for a chosen family: the family itself, then the Bangla faces, then
    /// what every device already has.
    /// </summary>
    public static string Stack(string family) =>
        $"'{family}', {BanglaFallback}, {SystemFallback}";

    /// <summary>
    /// The one Google Fonts stylesheet both families need. The Bangla fallbacks are always in it,
    /// so a page keeps rendering Bangla whichever Latin families are chosen.
    ///
    /// Weights are merged per family rather than listed per choice, because the same family is
    /// genuinely wanted more than once: Hind Siliguri is offered for both body and headings and
    /// is also the Bangla fallback, each with its own weights. Google does merge a repeated
    /// family itself and serves every weight asked for, so this is not working around a failure
    /// — it keeps the URL to one canonical, shorter form, which is what makes the browser cache
    /// hit across two people who chose the same pair.
    /// </summary>
    public static string StylesheetUrl(FontChoice body, FontChoice display)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(display);

        // Sorted, so the same choices always give the same URL and the browser cache is not
        // split by the order the arguments happened to arrive in.
        var weightsByFamily = new SortedDictionary<string, SortedSet<int>>(StringComparer.Ordinal);

        Add(body.Family, body.Weights);
        Add(display.Family, display.Weights);
        Add("Hind Siliguri", BodyWeights);
        Add("Hind Siliguri", DisplayWeights);
        Add("Noto Sans Bengali", BodyWeights);

        var families = weightsByFamily.Select(entry =>
            $"family={entry.Key.Replace(' ', '+')}:wght@{string.Join(";", entry.Value)}");

        return "https://fonts.googleapis.com/css2?" + string.Join("&", families) + "&display=swap";

        void Add(string family, string weights)
        {
            if (!weightsByFamily.TryGetValue(family, out var set))
            {
                set = [];
                weightsByFamily[family] = set;
            }

            foreach (var weight in weights.Split(';'))
            {
                set.Add(int.Parse(weight, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
