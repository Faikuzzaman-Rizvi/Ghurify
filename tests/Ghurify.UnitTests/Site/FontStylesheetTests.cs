using System.Text.RegularExpressions;
using Ghurify.Domain.Site;

namespace Ghurify.UnitTests.Site;

/// <summary>
/// The web app's index.html carries a Google Fonts stylesheet so the first paint has the right
/// typefaces without waiting for the site configuration to arrive. The theme is configurable
/// though, so useAppliedSiteConfig injects the stylesheet for whatever fonts are actually set.
///
/// On the default theme — the common case — those two URLs have to be the same one, or the browser
/// fetches two stylesheets and downloads fonts nobody asked for. That is not a hypothetical: the
/// static link used to name a family the default theme does not use (Baloo Da 2) and an extra
/// Poppins weight, which put four unused font files on the page's critical path. The webfonts are
/// the slowest thing the page loads and the headings repaint when they land, so they decided LCP.
///
/// Nothing in a build would notice that drift, hence this test.
/// </summary>
public sealed class FontStylesheetTests
{
    [Fact]
    public void IndexHtml_AsksForExactlyTheDefaultThemesFonts()
    {
        var expected = FontChoices.StylesheetUrl(DefaultBody(), DefaultDisplay());
        var actual = FontStylesheetHrefInIndexHtml();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IndexHtml_HasOneFontStylesheet()
    {
        // Two would mean two render-blocking requests to the same origin for overlapping fonts.
        Assert.Single(FontStylesheetHrefs());
    }

    [Fact]
    public void TheDefaultFontsAreOnesTheThemeOffers()
    {
        // Guards the premise of the test above: a default that is not in the lists would fall back
        // to the first choice at runtime, and index.html would then be wrong in a way that only
        // showed up as a second stylesheet in the browser.
        Assert.NotNull(FontChoices.FindBody(DefaultFamily(SiteSettingKeys.FontBody)));
        Assert.NotNull(FontChoices.FindDisplay(DefaultFamily(SiteSettingKeys.FontDisplay)));
    }

    private static FontChoice DefaultBody() =>
        FontChoices.FindBody(DefaultFamily(SiteSettingKeys.FontBody)) ?? FontChoices.Body[0];

    private static FontChoice DefaultDisplay() =>
        FontChoices.FindDisplay(DefaultFamily(SiteSettingKeys.FontDisplay)) ?? FontChoices.Display[0];

    /// <summary>The shipped default for one setting, straight from the catalogue.</summary>
    private static string DefaultFamily(string key) => SiteSettingsCatalog.Defaults()[key];

    private static string FontStylesheetHrefInIndexHtml() => Assert.Single(FontStylesheetHrefs());

    /// <summary>Every Google Fonts stylesheet href in index.html, in document order.</summary>
    private static List<string> FontStylesheetHrefs()
    {
        var html = File.ReadAllText(Path.Combine(FindWebAppDirectory(), "index.html"));

        return [.. Regex
            .Matches(html, @"href=""(?<href>https://fonts\.googleapis\.com/[^""]+)""", RegexOptions.IgnoreCase)
            .Select(match => match.Groups["href"].Value)];
    }

    private static string FindWebAppDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "web", "ghurify-web");
            if (File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate web/ghurify-web by walking up from " + AppContext.BaseDirectory);
    }
}
